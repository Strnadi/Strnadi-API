# GDPR technický audit — Strnadi-API (v2)

Toto je **technická kontrola kódu, nikoli právní posouzení**. Všechny nálezy je třeba
prodiskutovat s DPO/právníkem.

## Shrnutí

Projekt je ornitologická aplikace se záznamem geolokace pozorování. Byla zjištěna řada
závažných technických rizik: veřejný (bez autentizace) přístup ke geodatům svázaným
s `UserId`; PII v logách; chybějící retenční mechanismy; chybějící endpointy pro
export/anonymizaci dat; nevynucované pole souhlasu (consent).

---

## 1. Osobní údaje (PII): entity a úložiště

`src/Tenant/Tenant.Domain/Entities/User.cs`
- Pole: `Email`, `FirstName`, `LastName`, `Password` (hash), `City`, `PostCode`,
  `Appleid`, `GoogleId`, `Nickname`, `Consent` (řádky 7–35). Vše v databázi v čitelné
  podobě, šifrování na úrovni aplikace chybí (spoléhá se na šifrování disku/DB na
  infrastrukturní úrovni — z kódu neověřitelné, nutno potvrdit s infra týmem).

`src/Tenant/Tenant.Domain/Entities/RecordingPart.cs:13-19`
- `GpsLatitudeStart/End`, `GpsLongitudeStart/End` — přesné GPS souřadnice. Přes
  `Recording.UserId` (`Recording.cs:21`) přímo svázané s konkrétním uživatelem →
  jedná se o osobní údaj o poloze (citlivější kategorie v kontextu GDPR — odhaluje
  pohyb osoby).

`src/Tenant/Tenant.Domain/Entities/Device.cs`
- `FcmToken` — identifikátor zařízení/push notifikací, nepřímé PII, odesílá se do
  Firebase (Google, třetí strana, viz bod 6).

### Riziko (vysoké): veřejný přístup ke geolokaci bez autentizace

`src/Tenant/Tenant.Api/Controllers/RecordingsController.cs:14-19` (`GetAllAsync`) a
`:33-39` (`GetByIdAsync`) — **chybí `[Authorize]`**. Kdokoli anonymně může zavolat
`GET /recordings?userId=X&parts=true` a získat přesné GPS body (start/end), poznámky,
datum — svázané s konkrétním `UserId` (`RecordingResponse.cs:12` obsahuje `UserId`
přímo v odpovědi).

`src/Tenant/Tenant.Api/Controllers/MapClustersController.cs:12-16` — také bez
`[Authorize]`, přijímá `userId` jako filtr (řádek 29) → umožňuje anonymně sestavit
mapu pohybu konkrétního uživatele (při vysokém zoomu se clustery rozpadají na
jednotlivé body, viz `MapClustersService.cs:55-67`, „leaf" clustery vrací
`MapClusterItem` s přesnými lat/lng).

To v podstatě umožňuje bez autentizace deanonymizovat a sledovat historii polohy
konkrétního `UserId`.

**Doporučení:** vyžadovat `[Authorize]` minimálně pro filtr podle
`userId`/pro nezakryté (leaf) údaje, nebo explicitně omezit veřejné API na
agregované/zaokrouhlené hodnoty, pokud je veřejný přístup záměrnou součástí produktu
(mapa pozorování) — a zdokumentovat právní základ (oprávněný zájem/veřejný vědecký
projekt) v zásadách ochrany osobních údajů.

---

## 2. Logování PII v čitelné podobě

`src/Tenant/Tenant.Infrastructure/Email/SmtpEmailSender.cs:51`
```csharp
logger.LogError(e, "Failed to send email to {Email}", toEmail);
```
E-mailová adresa se zapisuje do logu při chybě odeslání.

`src/Tenant/Tenant.Application/Notifications/NotificationsService.cs:36`
```csharp
logger.LogError(ex, "Failed to send notification to device {FcmToken}", device.FcmToken);
```
FCM token zařízení (nepřímý identifikátor uživatele) se zapisuje do logu.

**Důležité (násobící riziko):** `src/ServiceDefaults/Extensions.cs:51-55`
```csharp
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});
```
a řádky 83–99 (`AddOpenTelemetryExporters`) — pokud je nastavena proměnná
`OTEL_EXPORTER_OTLP_ENDPOINT`, všechny logy (včetně již sestavené zprávy s
e-mailem/FcmToken výše) se exportují do externího/centralizovaného OTLP kolektoru.
Jde fakticky o přenos PII do telemetrického systému/potenciálně třetí straně —
vyžaduje samostatné právní posouzení a případně DPA s provozovatelem kolektoru.

Rovněž tam, řádky 64–75: `AddAspNetCoreInstrumentation` trasuje HTTP požadavky
včetně URL/query stringu. Endpoint `GET /users/exists?email=...`
(`UsersController.cs:97-110`) předává e-mail v query stringu → objeví se v
`http.target`/`http.url` atributech trace dat i ve standardních access-logách
webserveru/reverse-proxy.

**Doporučení:** přesunout e-mail do těla POST požadavku, nebo
explicitně vyloučit takové endpointy/parametry z instrumentace
(`tracing.Filter`, obdobně jako filtr health-checků na řádcích 69–71).

`src/Tenant/Tenant.Api/Logging/CompactConsoleFormatter.cs:1-31` — vlastní konzolový
formatter počítá `message` (řádek 10), ale nikdy jej nezapíše do `textWriter` (píše
jen úroveň/kategorii + exception, pokud existuje, řádky 25–29). Kvůli tomu nejsou
výše uvedené LogError zprávy v konzoli vidět — to snižuje riziko pro tento konkrétní
sink, ale vypadá to spíš jako neúmyslná chyba formatteru než vědomá kontrola PII
(a nechrání to proti OTel exportu, který používá jinou cestu).

**Co je uděláno správně:** hesla/tajemství se nikde přímo nelogují (nenalezeno žádné
`LogXxx` s `Password`/tokenem z JWT).

---

## 3. Retence / TTL

- Nebyl nalezen žádný background job / hosted service / cron pro čištění/anonymizaci
  dat (hledání `BackgroundService`, `IHostedService`, `Cron`, `Hangfire` — 0 výsledků).
- `Recording.Deleted` (`Recording.cs:23`) — příznak soft-delete, ale neexistuje
  automatická úloha pro finální smazání po uplynutí retenční lhůty; finální smazání
  jen ručně přes admin endpoint (`RecordingsController.cs:41-52`, vyžaduje
  `final=true` a admina).
- Neověřené účty (`IsEmailVerified == false`, `User.cs:19`) nemají TTL/automatické
  čištění — uživatel se může zaregistrovat (email/jméno/město atd. se již uloží do
  DB, `AuthService.cs:55-75`), nikdy nepotvrdit e-mail, a data zůstanou navždy.
- JWT tokeny (`JwtTokenService.cs:25-32`) mají `expires` podle
  `jwtSettings.Lifetime` (konfigurovatelné, `JwtSettings.cs:17-18`) — TTL pro session
  existuje, to je v pořádku. Stejný mechanismus se ale používá i pro verifikační
  odkaz (`LinkBuilder.cs:7-8`) a odkaz na reset hesla (`LinkBuilder.cs:13-14`) —
  dědí společný `Jwt:Lifetime`, což může být delší, než je vhodné pro jednorázový
  odkaz na reset hesla (chybí kratší samostatné TTL/jednorázovost — token lze použít
  opakovaně až do vypršení `Lifetime`).

**Doporučení:** přidat retenční politiku (job) pro: neověřené účty starší N dní,
soft-deleted recordings starší N dní (finální smazání nebo anonymizace), zastaralé
`Device`/FCM tokeny.

---

## 4. Souhlas (Consent)

`src/Tenant/Tenant.Domain/Entities/User.cs:21` — `public bool? Consent { get; set; }`.
`src/Tenant/Tenant.Application/Auth/SignUpRequest.cs:11` — `bool Consent` v
požadavku na registraci.
`src/Tenant/Tenant.Application/Auth/AuthService.cs:63` — hodnota se prostě uloží
(`Consent = request.Consent`), **bez jakékoli serverové validace**, že
`Consent == true`. Technicky lze zaregistrovat s `Consent: false` a účet se přesto
vytvoří, e-mail se odešle, data se zpracovávají.

Dále chybí:
- Časové razítko/verze souhlasu (pole `ConsentDate`, `ConsentVersion` neexistují) —
  při sporu nelze prokázat, kdy a s jakou verzí zásad uživatel souhlasil
  (čl. 7(1) GDPR vyžaduje možnost prokázat souhlas).
- Samostatný souhlas pro citlivější toky dat (push notifikace přes Google Firebase,
  telemetrie/analytika přes OTel) — souhlas je jedno obecné boolean pole na vše.
- Tok Google/Apple OAuth (`AuthService.cs:125-181`, 183-223) vůbec explicitně
  nenastavuje `Consent` při vytváření uživatele v `SignUpGoogleAsync`/`GoogleAsync`/
  `AppleAsync` — tyto metody nevytvářejí `User` s `Consent`, je třeba ověřit celou
  cestu registrace přes sociální sítě (nenalezeno explicitní `Consent =` při
  sociální registraci).

**Doporučení:** udělat `Consent` vynucenou (enforced) podmínkou vytvoření účtu, přidat
`ConsentGivenAt`/`ConsentVersion`, rozdělit souhlasy podle kategorií účelu
zpracování.

---

## 5. Právo na výmaz / export (erasure / portability)

- Endpoint pro export dat uživatele (přenositelnost údajů, čl. 20) —
  **nenalezen** (hledání `export`/`portability` — 0 shod v business kódu).
- Smazání účtu existuje: `src/Tenant/Tenant.Api/Controllers/UsersController.cs:56-67`
  → `UsersService.DeleteAsync` (`UsersService.cs:48-57`) → `users.Remove(user)`
  (`UsersRepository.cs:54-57`) — jde o **hard delete** uživatele, kaskádově mažící
  související záznamy díky `DeleteBehavior.Cascade` v `TenantDbContext.cs`
  (řádky 139, 232, 241, 381, 444 aj. — Devices, Photos, Recordings atd. se mažou
  kaskádově).
  - Plus: to fakticky naplňuje „právo na výmaz".
  - Minus: chybí možnost anonymizace místo úplného kaskádového smazání — pokud
    záznamy pozorování (Recording/RecordingPart) mají vědeckou/veřejnou hodnotu
    (jde o ornitologickou aplikaci s mapou pozorování), úplné kaskádové smazání
    zničí i vědecká data bez možnosti je zachovat v anonymizované podobě (což je
    často preferovanější i z pohledu produktu, i podle čl. 17(3) GDPR lze
    ponechat anonymizovaná data pro statistické/vědecké účely). Nyní tato volba
    neexistuje.
  - Rovněž se nekontroluje explicitní potvrzení (re-auth/heslo) před nevratným
    smazáním — pouze standardní JWT autorizace jako u jakéhokoli jiného
    zápisového endpointu.

**Doporučení:** přidat export endpoint (JSON export všech dat spojených s uživatelem:
profil, recordings, devices, achievements) a zvážit anonymizaci (vynulování
`UserId`/přesnosti GPS) jako alternativu k úplnému kaskádovému smazání záznamů
pozorování.

---

## 6. Předávání třetím stranám

- **Firebase Cloud Messaging (Google)** —
  `src/Tenant/Tenant.Infrastructure/Notifications/FirebaseNotificationService.cs:63`
  — `FcmToken` zařízení jde na `fcm.googleapis.com`. Potřeba právního
  základu/DPA s Google.
- **Mapy.cz** —
  `src/Tenant/Tenant.Infrastructure/MapyCz/MapyCzProxyService.cs:10-13` — proxy
  přeposílá query string na `api.mapy.cz`, včetně API klíče a případně souřadnic
  výřezu mapy uživatele. Dobře, že jde o server-side proxy (IP adresa uživatele se
  Mapy.cz přímo neodhaluje), ale samotné souřadnice zobrazení se mohou odhalovat.
- **SMTP poskytovatel** (`SmtpEmailSender.cs`) — e-mail, jméno (nickname) jdou přes
  nakonfigurovaný SMTP server (třetí strana v závislosti na hostingu).
- **OTLP/telemetrie** — viz bod 2, potenciální přenos PII (skrz zprávy v logách) do
  externího kolektoru při nastavené `OTEL_EXPORTER_OTLP_ENDPOINT`.
- **Google/Apple Sign-In** — `GoogleIdTokenValidator.cs`, `AppleIdTokenValidator.cs`
  — validace ID tokenů probíhá přes oficiální knihovny/veřejné klíče, to je
  standardní a očekávané.

**Doporučení:** sestavit/aktualizovat záznamy o činnostech zpracování (ROPA) se
seznamem všech výše uvedených třetích stran a potvrdit existenci právních
základů/DPA.

---

## 7. Výchozí nastavení / minimalizace dat

- `GET /recordings` a `GET /recordings/map-clusters` jsou ve výchozím stavu
  dostupné bez autentizace a bez omezení plochy/detailu (viz bod 1) — není to
  minimalizace dat ve výchozím nastavení.
- `GET /users/{userId}/get-profile-photo` (`UserPhotosController.cs:24-31`) —
  veřejně dostupné bez `[Authorize]`; možná záměrně (veřejný profil), ale je
  třeba potvrdit, že jde o produktové rozhodnutí, nikoli opomenutí.
- `UsersService.GetUserByIdAsync` (`UsersService.cs:20-27`) a `UpdateAsync`
  (řádky 29–46) správně skrývají e-mail před ostatními
  (`canSeeEmail = isAdmin || callerId == user.Id`) — to je příklad správné
  výchozí minimalizace dat.
- JWT (`JwtTokenService.cs:17-23`) vkládá e-mail do claimu tokenu v čitelné
  (nešifrované, pouze podepsané) podobě — token se předává klientovi a při
  každém dalším požadavku v hlavičce Authorization; jelikož JWT lze snadno
  dekódovat (base64), e-mail je fakticky dostupný komukoli, kdo má token
  klienta (např. pokud unikne přes logy proxy/CDN). Minimalizace: zvážit
  nevkládání e-mailu do claimů, používat pouze `sub` (userId) a e-mail
  dotahovat na backendu dle potřeby.

---

## Co je uděláno správně

- Hesla se hashují přes BCrypt (`BCryptPasswordHasher.cs:7`), neukládají se ani
  neloguji v čitelné podobě.
- E-mail je skryt před ostatními uživateli v `UsersService` (viditelnost e-mailu
  omezena na vlastníka/admina).
- JWT tokeny mají TTL (`JwtSettings.Lifetime`), server validuje
  issuer/audience/podpis/lifetime (`JwtTokenService.cs:38-48`).
- Tajemství (SMTP, JWT, connection string DB) nejsou zapsána v
  `appsettings.json`/`appsettings.Development.json` — načítají se z
  konfigurace/secrets.
- Mechanismus soft-delete pro recordings (`Recording.Deleted`) existuje jako
  mezikrok před nevratným smazáním.
- Pole `Consent` v modelu User existuje (i když není vynucené, viz bod 4) —
  infrastruktura pro zaznamenání souhlasu je připravena.

---

## Prioritní doporučení

1. **Vysoká priorita**: uzavřít anonymní přístup ke geolokačním datům
   (`RecordingsController.GetAllAsync/GetByIdAsync`, `MapClustersController.GetAsync`),
   nebo explicitně zdokumentovat/zdůvodnit veřejnost jako produktové rozhodnutí s
   omezením detailu.
2. **Vysoká priorita**: odstranit PII (e-mail, FcmToken) z textů log zpráv
   (`SmtpEmailSender.cs:51`, `NotificationsService.cs:36`), zejména vzhledem k
   exportu logů přes OpenTelemetry/OTLP (`ServiceDefaults/Extensions.cs:51-55`).
3. **Střední priorita**: implementovat vynucený souhlas (blokovat registraci bez
   `Consent=true`, přidat `ConsentGivenAt`/`ConsentVersion`), ověřit cestu Google/
   Apple auth z hlediska zaznamenání souhlasu.
4. **Střední priorita**: přidat endpoint pro export dat uživatele (přenositelnost
   údajů) a zvážit anonymizaci místo kaskádového hard-delete pro vědecká data
   pozorování.
5. **Střední priorita**: přidat retenční job (čištění neověřených účtů, finální
   výmaz soft-deleted recordings, odvolání neaktuálních FCM tokenů).
6. **Nízká priorita**: přesunout `email` z query stringu
   (`GET /users/exists?email=`) do těla požadavku, aby se neobjevoval v
   access-logách/trasování; přehodnotit obsah claimů v JWT (nezahrnovat e-mail do
   nešifrovaných claimů).
