# План повного перепису Strnadi API (EF Core, з нуля)

Дата: 2026-08-31

Базується на [`backend-review.md`](./backend-review.md) (знахідки поточної кодової бази) та
[`api-endpoints.md`](./api-endpoints.md) (84 ендпоінти в 10 контролерах — повний перелік поточного
публічного контракту, який новий бекенд має зберегти). Конкретних нових фіч поки немає — архітектура
свідомо закладається розширюваною, а не під конкретний список майбутніх фіч.

Ключовий принцип: **перезаписуємо застосунок, не базу даних одночасно.** Поточна Postgres-схема вже
живе (є дані користувачів, записів, тощо) — тому спершу EF мапиться на існуючу схему (Database-First /
scaffold), і лише після стабілізації нового коду схема еволюціонує через EF Migrations. Одночасний
редизайн і схеми, і застосунку — гарантований спосіб не здати проєкт вчасно.

---

## 1. Цільова архітектура

### 1.1 Шари (замість "фіча = окремий csproj без внутрішніх шарів")

Поточна структура (`Achievements/`, `Articles/`, `Auth/`, ... кожен свій `.csproj` з контролером
і всім упереміш) залишається як **модульний поділ**, але кожен модуль отримує внутрішні шари:

```
Strnadi.Domain          — сутності, value objects, доменні правила, без залежностей від EF/ASP.NET
Strnadi.Infrastructure  — EF Core DbContext, конфігурації сутностей, міграції, зовнішні сервіси
                          (FFmpeg, AI-класифікатор, Firebase, Email, JWT)
Strnadi.Application     — use-case сервіси по модулях (Articles, Auth, Recordings, ...), DTO,
                          валідація (FluentValidation), інтерфейси репозиторіїв/UoW
Strnadi.Api (Host)      — тонкі контролери, middleware, auth policies, Swagger
```

Модулі (Articles, Auth, Recordings, ...) — це **папки/неймспейси всередині Application та Domain**,
а не окремі `.csproj`. Поточний розподіл на 10+ csproj — зайва церемонія для проєкту такого розміру:
кожен новий метод вимагає жонглювання project references. Один `Domain`, один `Application`, один
`Infrastructure`, один `Api` — простіше збирати, простіше рефакторити межі модулів, коли вони ще не
устаканились.

### 1.2 Нарізка на модулі (bounded contexts) на основі `api-endpoints.md`

| Модуль | Що входить | Примітка |
|---|---|---|
| **Identity** | Auth (14 ендпоінтів) + Users (10) | Auth і Users зараз штучно розділені, хоча Auth повністю оперує сутністю User. Об'єднати в один модуль. |
| **Articles** | Articles + категорії + переклади (21) | Найчистіший модуль зараз, добре підходить як "pilot" нового підходу. |
| **Achievements** | Achievements (3) | Потребує редизайну умови нарахування (див. §3.3). |
| **Recordings** | Recordings + FilteredRecordings + Dialects + Detected Dialects (25) | Найбільший і найскладніший модуль. Розбити всередині на під-агрегати: `Recordings`, `RecordingParts`, `Dialects`. |
| **Media** | Photos (1) + профільні фото користувача | Зараз розмазано між `PhotosController` і `UsersController`. Об'єднати. |
| **Devices & Notifications** | Devices (3) + send-notification з Utils | Зараз push-нотифікації випадково лежать в `UtilsController`. Виділити окремо. |
| **Ops** | health, map-proxy, maintenance-задачі з Utils | Дивись §3.7 — частину варто взагалі прибрати з публічного HTTP API. |

---

## 2. Наскрізні (cross-cutting) рішення — робимо один раз, використовують усі модулі

### 2.1 Дані: EF Core замість Dapper

- `AppDbContext` (можливо декілька — окремо для домену Ops/Quartz, якщо потрібно) реєструється як
  **Scoped**, один на HTTP-запит. Це автоматично закриває [backend-review.md, п.4] — одне з'єднання на
  запит, а не по одному на кожен `[FromServices]`-репозиторій.
- Explicit транзакції (`DbContext.Database.BeginTransactionAsync` або просто один `SaveChangesAsync()`
  після зміни кількох агрегатів у межах use-case) — закриває приклад з
  `PostConfirmedDialectAsync`, де зараз два записи без транзакції.
- Спочатку — `dotnet ef dbcontext scaffold` з поточної Postgres-бази, щоб гарантовано не втратити
  жодного стовпця/зв'язку. Ручний рефакторинг entity-класів — окремим кроком після scaffold, не разом.
- Репозиторії (якщо залишаємо цей патерн) — тонкі обгортки над `DbSet<T>` + LINQ, ніякого raw SQL,
  окрім свідомо задокументованих виключень (складна аналітика).

### 2.2 Обробка помилок — прибираємо `ExecuteSafelyAsync`

[backend-review.md, п.2] — найшкідливіший патерн зараз. Заміна:

- Репозиторії/сервіси **не ковтають винятки**. Кидають доменні винятки (`NotFoundException`,
  `ConflictException`, `ValidationException`, `ForbiddenException`) або повертають `Result<T>`/
  `OneOf<T, Error>` — обрати один підхід на весь проєкт і не змішувати.
- Один `ExceptionHandlingMiddleware` (або `IExceptionHandler` в ASP.NET Core 8+) мапить доменні
  винятки на **`ProblemDetails` (RFC 7807)** з правильним HTTP-кодом. Жодних `BadRequest("рядок")` /
  `StatusCode(500, "рядок")` вручну по контролерах.
- Чіткі правила кодів (зараз плутанина, [backend-review.md, п.7]):
  - `401` — не автентифікований (немає/невалідний JWT). **Не** `400`, як зараз в багатьох місцях.
  - `403` — автентифікований, але немає прав (не admin, не власник ресурсу). Зараз для цього теж
    використовується `401` — виправити.
  - `404` — ресурс не знайдено.
  - `409` — реальний конфлікт (наприклад, email вже зайнятий). **Не** "не знайдено", як зараз в
    `FilteredRecordingsController.GetFilteredPartAsync`.

### 2.3 Автентифікація/авторизація — прибираємо copy-paste JWT-перевірки

[backend-review.md, п.3] — ручна перевірка JWT дублюється в ~50 екшенах. Заміна:

- `AddAuthentication().AddJwtBearer(...)` — стандартна ASP.NET Core автентифікація замість ручного
  `GetJwt()` + `TryValidateToken`.
- `[Authorize]` на контролері/екшені замість `if (jwt is null) return BadRequest(...)`.
- Policy `"AdminOnly"` (`RequireClaim` або кастомний `IAuthorizationHandler`, що читає роль з БД/claim)
  замість `if (!await usersRepo.IsAdminAsync(email)) return Unauthorized(...)` у кожному екшені.
- Resource-based authorization (`IAuthorizationHandler<Recording>`) для кейсів "власник або admin"
  (видалення/редагування recording, users) — зараз ця логіка руками повторюється в кожному методі.
- Ендпоінти без авторизації (`[AllowAnonymous]`) — явно позначені, а не "просто немає перевірки", як
  зараз з `ResetPasswordAsync`.

### 2.4 DTO замість Entity в API-контракті

[backend-review.md, п.6] — `user.Password = null!` перед `Ok(user)`. Заміна: окремі DTO-класи на
кожен модуль (`UserResponse`, `ArticleResponse`, ...), мапляться з Entity через ручні extension-методи
або невеликий mapper (Mapster/AutoMapper — обрати один, не обидва). Entity ніколи не повертається з
контролера.

### 2.5 Валідація запитів

FluentValidation-валідатори на кожен request DTO, підключені через
`AddFluentValidationAutoValidation()` — прибирає розкидані по репозиторіях
`string.IsNullOrWhiteSpace(...)`-перевірки. Невалідний запит → `400` з `ProblemDetails`, автоматично,
без ручного коду в екшені.

### 2.6 Логування

Один підхід: `ILogger<T>` через DI всюди. Прибрати статичний `Shared.Logging.Logger` повністю —
[backend-review.md, п.9] описує, як через нього легко загубити логи (синхронний `Console.Write`
поруч із буферизованим `ILogger`-провайдером дає різну поведінку при падінні процесу — саме це,
ймовірно, і спостерігалось під час дебагу черги класифікації).

### 2.7 Swagger — генерація з коду, а не ручний YAML

XML doc-коментарі вже додані до всіх екшенів (`<summary>`/`<param>`/`<returns>`, комміт від
2026-08-31). Наступний крок:
- `GenerateDocumentationFile=true` в `.csproj` API-проєкту.
- `AddSwaggerGen()` + `IncludeXmlComments(...)` + `[ProducesResponseType]` на екшенах.
- Ручний `StrnadiAPI-openapi.yaml` виводимо з ужитку після звірки згенерованої специфікації з ним
  (він зараз джерело правди для деяких вже задокументованих ендпоінтів — звірити перед видаленням).

### 2.8 Пагінація

Списки (`GET articles`, `GET recordings`, `GET users`, `GET recordings/filtered`, `GET
recordings/filtered/detected/`) отримують `page`/`pageSize` query-параметри й `IQueryable` + `Skip`/
`Take` на рівні EF, а не завантаження всієї таблиці в пам'ять.

### 2.9 CancellationToken

Кожен `async` метод сервісу/репозиторію приймає `CancellationToken`, прокинутий від
`HttpContext.RequestAborted` — зараз відсутній усюди.

---

## 3. Особливі рішення по модулях

### 3.1 Identity (Auth + Users)

- `JwtService` лишається, але виносимо у `Infrastructure` як `ITokenService`.
- Соціальні логіни (Google/Apple) — зараз логіка гілкування прямо в `AuthController.GoogleAuth`/
  `LoginViaApple` (~70-100 рядків розгалужень в контролері). Виносимо в
  `IExternalAuthService`/`GoogleAuthService`/`AppleAuthService` в `Application`, контролер лишає 3-5
  рядків виклику.
- Пошук користувача по email — уніфікувати нормалізацію (зараз `SignUpAsync`/`LoginAsync` роблять
  `.ToLower()`, а `GetUserByEmailAsync` — ні; потенційний баг з регістром зберігається "як є" при
  переписі, якщо не виправити явно).

### 3.2 Articles

Найпростіший модуль, без відомих архітектурних проблем окрім загальних (§2). Гарний кандидат
для першого модуля нового підходу — на ньому обкатується весь наскрізний інструментарій (EF
scaffold, exception middleware, DTO+validation, Swagger) без ризику для складної логіки.

### 3.3 Achievements — редизайн умови нарахування

[backend-review.md, п.1] — критична знахідка: `achievements.sql` зберігає й виконує довільний SQL
з тіла admin-запиту. **Не переносити цей механізм у новий код як є.** Варіанти заміни:
- Enum передбачених критеріїв (`RecordingsUploadedCount >= N`, `DialectsConfirmedCount >= N`, ...),
  що мапляться на конкретні, наперед написані LINQ/EF-запити — новий критерій = новий case, не
  довільний SQL.
- Якщо потрібна гнучкість без релізу коду — розглянути JSON-based rule DSL (`{"metric": "recordings",
  "op": ">=", "value": 10}`), інтерпретований у застосунку, ніколи не SQL-текст.
Це рішення потребує погодження окремо — впливає на форму `POST achievements` (поле `sql` зникає).

### 3.4 Recordings / FilteredRecordings / Dialects — найбільший модуль

- Розбити `RecordingsRepository` (зараз "god repository": recordings + parts + dialects + audio
  processing + filtered parts, [backend-review.md, розділ "Méně závažné"]) на:
  `RecordingsRepository`, `RecordingPartsRepository`, `DialectsRepository`,
  `DetectedDialectsRepository` — кожен свій агрегат.
- Аудіо-обробка (`FFmpegService`, `AiModelConnector`, `AudioProcessingQueue`/`AudioProcessingService`)
  переїжджає в `Infrastructure` як окремий підмодуль `AudioProcessing`.
- Черга класифікації: поточна `Channel`-based черга — in-memory, не персистентна (втрата задач при
  рестарті процесу), послідовна обробка без ретраїв. Оскільки `Quartz` вже є залежністю проєкту
  (використовується для `CheckRecordingJob`) — розглянути перенесення класифікації на Quartz-джоби
  замість власноруч написаної черги: отримуємо персистентність (Quartz має ADO.NET job store,
  `quartz-pg.sql` вже налаштований), ретраї з коробки, і один механізм фонових задач замість двох
  паралельних (Channel + Quartz).
- `Obsolete`-ендпоінти (`part/{recId}/{partId}/sound`, `delete-confirmed-dialect/{filteredPartId}`)
  — не переносити в новий API, якщо клієнти вже мігрували на заміну (перевірити перед видаленням).

### 3.5 Media (Photos)

Об'єднати `PhotosController` (recording photo) і фото-профіль-ендпоінти з `UsersController`
(`upload-profile-photo`, `get-profile-photo`) в один модуль `Media` — зараз штучно розділені,
хоча роблять одне й те саме (base64 фото → файл/БД) для різних сутностей.

### 3.6 Devices & Notifications

Виділити `send-notification` з `UtilsController` разом із `DevicesController` в модуль
`Notifications` — зараз push-розсилка випадково лежить у "звалищі для всього іншого" (`Utils`).

### 3.7 Ops (health, map-proxy, maintenance)

- `HEAD utils/health` → замінити на стандартний `AddHealthChecks()`/`MapHealthChecks("/health")` з
  перевіркою БД-з'єднання, а не голий `Ok()`.
- `map/{*path}` (проксі на Mapy.cz) — залишається як є, ізольований, без залежностей від решти
  домену.
- `fix-same-dates`, `normalize-existing-audios`, `analyze-parts` — це одноразові/рідкісні
  maintenance-скрипти, а не частина довготривалого публічного API. Розглянути винесення в окремий
  CLI-інструмент (`dotnet run --project Tools -- fix-same-dates`) замість HTTP-ендпоінтів —
  прибирає ризик випадкового виклику через мережу і не засмічує Swagger-специфікацію продукту.
  Якщо мають лишитись HTTP-ендпоінтами — принаймні прибрати з публічної специфікації (internal-only
  route group).

---

## 4. Тести (зараз відсутні повністю)

У репозиторії немає жодного тестового проєкту. Для повного перепису це критично — без тестів
неможливо підтвердити, що новий бекенд поводиться так само, як старий, для 84 існуючих ендпоінтів.

- `Strnadi.Tests.Unit` — доменна логіка, валідатори, мапери, без БД.
- `Strnadi.Tests.Integration` — `WebApplicationFactory` + Testcontainers (Postgres) — по одному
  набору тестів на модуль, мінімум happy-path + основні коди помилок з `api-endpoints.md`.
- Контрактні тести проти `api-endpoints.md`/старого `StrnadiAPI-openapi.yaml` — гарантія, що
  маршрути/статус-коди не розʼїхались із задокументованою поведінкою під час перепису.

---

## 5. Порядок виконання (етапи)

1. **Каркас**: нова структура `Domain`/`Application`/`Infrastructure`/`Api`, EF scaffold з поточної
   БД, `ExceptionHandlingMiddleware` + `ProblemDetails`, JWT-автентифікація + policies, Swagger з
   XML-коментарів, health checks. Без бізнес-логіки — інфраструктура, яку використовуватимуть усі
   модулі.
2. **Identity** (Auth + Users) — базовий модуль, від нього залежить авторизація решти.
3. **Articles** — pilot для обкатки підходу (DTO, валідація, EF-репозиторій) на простому модулі.
4. **Devices & Notifications**, **Media** — невеликі, малоризикові модулі.
5. **Achievements** — разом з редизайном умови нарахування (§3.3, потребує окремого рішення до
   старту).
6. **Recordings / FilteredRecordings / Dialects** — найбільший модуль, робити останнім з бізнес-
   модулів, коли всі наскрізні патерни вже перевірені на попередніх кроках. Включає перенесення
   аудіо-обробки й рішення по черзі класифікації (§3.4).
7. **Ops** — health checks, map-proxy (тривіальний перенос), рішення по maintenance-ендпоінтах.
8. **Cutover**: звірка згенерованого Swagger із `StrnadiAPI-openapi.yaml`, повний прогін
   інтеграційних тестів проти обох бекендів на однакових даних (по можливості — паралельний запуск
   старого й нового бекенду на staging), перемикання трафіку.

Кожен етап — окремий, тестований і мержабельний приріст; не чекаємо кінця перепису, щоб побачити,
чи працює новий підхід.
