# Plán generalizace platformy (Strnadi → obecná přírodovědecká platforma)

Datum: 2026-09-02

Navazuje na [`rewrite-plan.md`](./rewrite-plan.md) (přepis Strnadi API na EF Core / čistou architekturu).
Tento dokument popisuje další, větší krok: Strnadi přestává být jediným produktem a stává se jedním
z více dynamicky vytvářených "druhových" projektů na společné platformě (další v plánu např. sarančata).
Aktuálně jde o architektonický záměr, ne o hotové rozhodnutí do posledního detailu — otevřené body jsou
označené v §6.

---

## 1. Cílová architektura

Platforma se dělí na **control plane** (jeden sdílený Admin server) a **data plane** (libovolný počet
dynamicky vytvářených projektových instancí, dnes jen Strnadi).

```
                      ┌─────────────────────┐
                      │     Admin server     │  ASP.NET + Blazor
                      │  (control plane)      │
                      │  - uživatelé/admini   │
                      │  - role & oprávnění   │
                      │  - OAuth/OIDC (OpenIddict)
                      │  - správa projektů (set-only env)
                      └──────────┬───────────┘
                                 │ vytvoření projektu
                                 ▼
                      ┌─────────────────────┐
                      │ Infrastructure Manager│  mikroslužba
                      │ - přijme požadavek    │
                      │ - spawne Docker kontejner
                      │   na zvoleném hardwaru │
                      └──────────┬───────────┘
                                 ▼
                 ┌───────────────┴───────────────┐
                 ▼                                ▼
        ┌─────────────────┐              ┌─────────────────┐
        │ Strnadi (projekt) │              │ Sarančata (projekt) │  ...další druhy
        └─────────────────┘              └─────────────────┘
```

### 1.1 Admin server

- ASP.NET + Blazor, samostatný projekt vedle Strnadi.
- Vlastní **jediný seznam uživatelů a adminů** napříč všemi projekty (viz §2.1).
- Hostí vlastní OAuth/OIDC autorizační server (viz §2.2).
- Spravuje role a oprávnění (viz §2.3).
- Slouží ke správě jednotlivých projektů — administrace prostředí je **set-only**: admin panel zapisuje
  konfiguraci/environment projektu, nečte zpět živý stav (otevřená otázka reconciliace, viz §6).

### 1.2 Projektové servery (data plane)

- Každý druh (Strnadi, sarančata, ...) běží jako samostatná instance, dnes strukturálně stejná jako
  současný `Strnadi.Api`/`Application`/`Domain`/`Infrastructure`.
- Vlastní data (nahrávky, články, ...) zůstávají v projektu; identita a oprávnění se přebírají z Admin
  serveru.

### 1.3 Infrastructure Manager

- Mikroslužba, kterou Admin server volá ve chvíli, kdy oprávněný uživatel vyplní formulář "vytvořit
  projekt" a odešle ho.
- Úkol: spawnout Docker kontejner na zadaném hardwaru pro nový projekt.
- Bezpečnostně nejcitlivější komponenta celého návrhu — spouští kontejnery na základě akce v UI, fakticky
  jde o řízené spouštění kódu na infrastruktuře (viz rizika v §6).

---

## 2. Identita a přístup

### 2.1 Sdílená databáze uživatelů

- Uživatelé a admini se stěhují z jednotlivých projektů (dnes `Strnadi.Domain.Entities.User` s poli
  `Password`, `GoogleId`, `Appleid`, `Role` — viz `v2/Strnadi.Application/Auth/AuthService.cs`) na Admin
  server.
- Jeden účet je platný napříč všemi projekty (Strnadi i budoucí sarančata sdílí stejný seznam uživatelů).
- Projektové servery si identitu uživatele pouze ověřují (token z Admin serveru), nespravují ji.

### 2.2 Vlastní OAuth/OIDC server

- Admin server se stává vlastním OAuth/OIDC providerem pro všechny klientské aplikace (Strnadi app,
  budoucí aplikace pro další druhy, samotný admin panel).
- Implementace přes **OpenIddict** (ne psaní protokolu od nuly) — authorization code flow, PKCE,
  refresh/revoke tokeny řeší knihovna, ne vlastní kód.
- Google a Apple zůstávají jako upstream identity providery pro federované přihlášení — dnešní logika v
  `GoogleAsync`/`AppleAsync` (`AuthService.cs`) se přesune na Admin server a naváže na standardní ASP.NET
  Core authentication handlery místo ruční validace ID tokenů.
- Současné JWT vydávané přímo `Strnadi.Api` (`ITokenService.GenerateToken`) zaniká — tokeny vydává jen
  Admin server / OpenIddict.

### 2.3 Role a oprávnění

- Dnešní plochý string `User.Role` ("user", ...) se nahrazuje modelem **role = množina oprávnění**.
- Super admin (biolog) může vytvářet nové role kombinací existujících oprávnění — bez zásahu do kódu.
- Oprávnění musí pokrýt jak akce v rámci Admin serveru (správa projektů, uživatelů), tak akce uvnitř
  jednotlivých projektů (kdo smí co v rámci Strnadi/sarančat) — přesný rozsah/scoping oprávnění je
  otevřená otázka (§6).

---

## 3. Správa projektů

- Vytvoření projektu: oprávněný uživatel vyplní formulář v admin panelu → Admin server zavolá
  Infrastructure Manager → ten spawne Docker kontejner s novou instancí projektu.
- Správa běžícího projektu z admin panelu je **set-only** — panel mění konfiguraci/environment, ale
  (zatím) nečte živý stav kontejneru zpět.

---

## 4. .NET Aspire a reorganizace řešení

Aspire se používá jako orchestrace a "lepidlo" mezi vlastními službami platformy (Admin server, Strnadi,
do budoucna Infrastructure Manager) — service discovery, jednotný dashboard (logy/trace/metriky),
sjednocené service defaults. Neřeší produkční orchestraci uživatelských kontejnerů spouštěných
Infrastructure Managerem — to zůstává samostatná vrstva (Docker/K8s).

### 4.1 Cílová adresářová struktura

```
v2/
├── Strnadi.sln
├── src/
│   ├── AppHost/                 Aspire orchestrátor
│   ├── ServiceDefaults/         sdílené OpenTelemetry/health checks/resilience
│   ├── Admin/                   nový control-plane server
│   │   ├── Admin.Api/           ASP.NET + Blazor + OpenIddict server
│   │   ├── Admin.Application/
│   │   ├── Admin.Domain/
│   │   └── Admin.Infrastructure/
│   └── Projects/
│       └── Strnadi/             dnešní obsah v2/ (Strnadi.Api/Application/Domain/Infrastructure)
├── docs/
├── scripts/
└── artifacts/
```

### 4.2 Pořadí kroků

1. `git mv` současných `Strnadi.*` projektů do `v2/src/Projects/Strnadi/*`, oprava relativních
   `ProjectReference` cest, ověření že `dotnet build` prochází — samostatný "čistý přesun" commit.
2. Založení `AppHost` + `ServiceDefaults` (Aspire šablona) v `v2/src/`.
3. Napojení `ServiceDefaults` do `Strnadi.Api`, registrace `Strnadi.Api` jako resource v `AppHost`.
4. Až tohle běží — založení `Admin.*` projektů.

(Přesun se dělá **před** založením Aspire projektů, ne naopak — jinak se cesty v AppHostu musí opravovat
dvakrát.)

---

## 5. Fáze realizace

| Fáze | Obsah |
|---|---|
| 1 | Reorganizace `v2` řešení dle §4, zavedení Aspire (AppHost, ServiceDefaults) |
| 2 | Založení Admin serveru, přesun DB uživatelů, OpenIddict server + federace Google/Apple |
| 3 | Model rolí a oprávnění na Admin serveru, správa přes UI (super admin biolog) |
| 4 | Infrastructure Manager — vytváření projektů přes formulář, spawn Docker kontejnerů |
| 5 | Migrace Strnadi na nový identity/permission model, odstranění duplicitní auth logiky ze
    `Strnadi.Application/Auth` a `Strnadi.Infrastructure/Auth` |

Fáze na sobě závisí v tomto pořadí — Admin server (2) potřebuje hotovou strukturu z (1), role (3)
potřebují existující uživatele na Admin serveru z (2), Infrastructure Manager (4) potřebuje hotová
oprávnění z (3) (kdo smí vytvořit projekt), a until (4) nedává smysl migrovat Strnadi (5), protože
projekty ještě nejsou vytvářeny tímto mechanismem.

---

## 6. Otevřené otázky / rizika

- **Bezpečnost Infrastructure Manageru.** Spawnování Docker kontejneru na základě akce v UI je fakticky
  vzdálené spouštění kódu jako feature. Nutno vyřešit: kdo má přístup k Docker socketu/API, limity
  zdrojů, validace formuláře, síťová izolace cizích kontejnerů, případně fronta úloh místo synchronního
  volání.
- **Set-only správa environmentu.** Bez zpětného čtení živého stavu hrozí drift mezi tím, co admin panel
  "myslí", že platí, a realitou v kontejneru. Bude třeba zvážit alespoň základní reconciliaci/health
  reporting z projektů zpět na Admin server.
- **Scoping oprávnění.** Nejasné, zda jedna role platí globálně přes všechny projekty, nebo je vázaná na
  konkrétní projekt (uživatel admin ve Strnadi, ale ne v sarančatech) — ovlivňuje datový model rolí.
- **Produkční orchestrace kontejnerů.** Aspire řeší jen vlastní služby platformy v rámci vývoje, ne
  produkční běh dynamicky vytvářených projektů — konkrétní cílová platforma (holý Docker, Swarm, K8s)
  zatím nebyla zvolena.
