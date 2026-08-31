# Revize backendu Strnadi-API (před refaktoringem na EF Core)

Datum: 2026-08-31

Aktuální stav: přístup k datům je postavený na **Dapper + syrovém Npgsql** (žádné EF Core). Níže jsou připomínky ke kvalitě kódu, seřazené podle závažnosti, které je potřeba zohlednit při plánovaném přechodu na Entity Framework.

## Kritické

### 1. Uložené a přímo spouštěné SQL v `AchievementsRepository`
`Repository/AchievementsRepository.cs:135-141` — podmínka pro udělení achievementu se ukládá do sloupce `achievements.sql` jako text a později se spouští přímo:

```csharp
string sql = achievement.Sql;
var userIds = (await ExecuteSafelyAsync(Connection.QueryAsync<int>(sql)))?.ToArray();
```

Jde o doslova uložený a spouštěný libovolný SQL příkaz, který přichází z těla POST požadavku administrátora, bez parametrizace. I když je endpoint omezený jen na adminy, jde o architektonickou časovanou bombu — kompromitace jedné admin session znamená plný přístup k databázi přes libovolný SQL. Při přechodu na EF je nutné tento mechanismus úplně přepracovat — např. na explicitní enum/rule-engine podmínek, nikdy ne na holý SQL text.

### 2. `RepositoryBase.ExecuteSafelyAsync` — polykání všech výjimek
Globální try/catch obalující téměř každou metodu repozitáře, který **jakoukoliv** výjimku (bug, porušení unique constraintu, výpadek spojení, zrušení requestu) potichu promění na `false`/`null`/`default` a jen zaloguje text. Controller pak nemá šanci rozlišit "nenalezeno" od "databáze spadla" od "porušení integrity" — jen hádá HTTP status podle `null`/`false`. Toto je nejškodlivější vzor v codebase jak pro čistotu kódu, tak pro debugování produkce.

## Závažné

### 3. Kopírovaná JWT logika v každé akci
Prakticky všude se opakuje stejných ~15 řádků: `GetJwt()` → `BadRequest("No JWT provided")` → `TryValidateToken` → `Unauthorized()` → (někdy) `IsAdminAsync` → `Unauthorized(...)`. Duplicita napříč 60+ endpointy a sémantika už je nekonzistentní — chybějící token vrací jednou `400`, jinde není kontrola vůbec (např. `ResetPasswordAsync` JWT nevyžaduje). Mělo by být nahrazeno pomocí `[Authorize]` + JWT bearer scheme + policy-based autorizací (např. policy `AdminOnly`), ne ručními podmínkami v každé akci.

### 4. Jedno DB spojení na instanci repozitáře, ne na request
`RepositoryBase` otevírá `NpgsqlConnection` přímo v konstruktoru. Pokud jedna akce používá `[FromServices] UsersRepository` i `[FromServices] ArticlesRepository`, jde o dvě fyzicky oddělená spojení na jeden HTTP request, bez společné transakce mezi nimi. Příklad: `FilteredRecordingsController.PostConfirmedDialectAsync` volá `CreateFilteredPartAsync` a `InsertDetectedDialectAsync` jako dvě oddělené operace bez transakce — pokud druhá selže, první zůstane zapsaná. EF s `DbContext` na scope requestu a explicitními transakcemi tohle řeší přirozeně.

### 5. Dynamický UPDATE přes reflexi
`UsersRepository.UpdateAsync` skládá SQL příkaz podle atributů `[Column]` pomocí reflexe. Funguje, ale je to křehké, bez kontroly za compile-time, a duplikuje to, co EF Core change tracking dělá automaticky.

### 6. Databázové modely se vrací přímo jako API odpověď
Např. `user.Password = null!` před `Ok(user)`. Chybí oddělení Entity/DTO — únik perzistenčních polí do API kontraktu (serializuje se vše, co v třídě je, kromě toho, co je ručně vynulováno).

## Méně závažné, ale k zamyšlení

- **Nekonzistentní HTTP kódy** — `Conflict()`/409 se používá jak pro "již existuje", tak pro "nenalezeno" (`GetFilteredPartAsync` vrací `Conflict()`, když část neexistuje; `Exists` vrací 409, když uživatel **existuje**).
- **Byznys logika v controlleru** — Google/Apple OAuth větvení žije přímo v `AuthController`, ne ve service vrstvě — těžko testovatelné, tlusté akce.
- **Dva různé přístupy k logování** současně — statický `Logger.Log(...)` na části míst, `ILogger<T>` přes DI jinde (např. `RecordingsController`, `UtilsController`).
- **Chybí validace modelů** (DataAnnotations/FluentValidation) — kontroly typu `string.IsNullOrWhiteSpace` jsou ručně rozeseté uvnitř repozitářů místo validace na úrovni model bindingu.
- **`RecordingsRepository` jako "god repository"** — recordings + parts + dialects + audio processing + filtered parts v jedné třídě. Při přechodu na EF stojí za zvážení rozdělení podle agregátů.
- **Seznamové endpointy bez stránkování** (`GetUsers`, articles, recordings) — načítají celou tabulku najednou.

## Dopad na refaktoring na EF Core

Samotný přechod na EF vyřeší:
- **bod 4** (DbContext na scope requestu, unit of work),
- **bod 5** (change tracking místo reflexního UPDATE),
- částečně **bod 6** (přes explicitní DTO a `.Select()`/AutoMapper).

**Body 1, 2 a 3 se přechodem na EF nevyřeší samy od sebe** — jde o samostatná architektonická rozhodnutí, která je potřeba do plánu refaktoringu zahrnout explicitně. Jinak dojde jen k přepsání stejných antivzorů nad EF.
