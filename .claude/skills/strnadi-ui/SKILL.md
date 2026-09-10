---
name: strnadi-ui
description: Use whenever creating or editing UI in this repo — Razor Pages under v2/src/Administration/Administration.Api/Pages, the future Blazor admin panel, or any CSS in wwwroot. Applies the Strnadi design system (tokens, components, layouts) so every page stays visually consistent instead of drifting per-page.
---

# Strnadi UI

Source of truth for design decisions: `v2/docs/design-system.md`. The runnable version of it is
`v2/src/Administration/Administration.Api/wwwroot/design-system.css`. This skill does not repeat
either file's content — it tells you how to use them so the two never drift apart.

## Before writing any markup or CSS

1. Read `v2/docs/design-system.md` in full — tokens, components, and the two page layouts (auth
   shell / admin panel shell) are all defined there.
2. Skim `wwwroot/design-system.css` to confirm the classes you're about to use actually exist
   with that name today — the doc is the design intent, the CSS file is the current reality;
   if they've diverged, the CSS file wins and the doc needs fixing (see § Extending below).
3. Look at an existing page that already uses the system before writing a new one:
   `Pages/Account/Login.cshtml`, `Register.cshtml`, `ResetPassword.cshtml`, and the shared
   `Pages/Account/_ExternalProviders.cshtml` partial are the canonical examples of how `asp-for`
   markup wires into `.ss-field`/`.ss-alert`/`.ss-btn` classes in practice.

## Rules

- Never hardcode a color, spacing value, font-size, or radius in markup or component CSS. Always
  use a `var(--...)` token or an existing `.ss-*` class. If a value you need has no token, that's
  a sign to add one (see below), not to inline a raw hex/px value.
- Reuse a component class before inventing a new pattern. A new admin-panel screen almost always
  needs `.ss-card` + `.ss-table` + `.ss-btn` + `.ss-badge`, not new one-off CSS.
- Auth pages (Razor Pages, centered `.ss-auth-shell`) and the admin panel (sidebar + topbar shell)
  share the *same* `design-system.css` and the *same* component classes. Do not fork styles per
  surface — the whole point is one design language for both, including the "manage my own
  account" screens a non-admin user reaches through the panel shell.
- Keep using plain framework-agnostic `.ss-*` classes (not scoped `.razor.css` files, not inline
  `style=`) so the same markup pattern works in both `.cshtml` and future `.razor` files.
- A validation/alert block always gets both the ASP.NET tag-helper behavior classes
  (`asp-validation-summary`/`asp-validation-for`) *and* the `ss-alert`/`ss-field__error` classes —
  see `Login.cshtml` for the exact pairing. Don't drop one for the other.

## Extending the system

When a screen needs something not yet in the system (a new component, a new semantic color, a
new layout piece):

1. Add it to `wwwroot/design-system.css` using the existing token variables — don't introduce a
   new raw value if an existing token already fits.
2. Add the same addition to `v2/docs/design-system.md` in the same change (§ Tokens or
   § Components, matching the doc's existing structure), so the doc keeps describing reality.
3. If the addition changes an *existing* token's value rather than adding a new one, say so
   explicitly before doing it — it likely affects every page that already shipped.

## Localization

Czech (`cs`) is the default/primary language (strnadi.cz); English (`en`) is the only other
supported UI language right now. All UI text goes through this, never a hardcoded literal:

- Shared strings live in `Administration.Api/Resources/SharedResource.resx` (Czech — this is the
  neutral/fallback resx, so it holds Czech directly, not English) and
  `SharedResource.en.resx` (English translation). Add a key to **both** files in the same change
  — a missing key falls back to displaying the raw key name to users, which is worse than not
  localizing at all.
- In `.cshtml` files: `L["KeyName"]` is available everywhere without an explicit `@inject` — it's
  set up once in `Pages/_ViewImports.cshtml`. Use it for headings, labels, buttons, links — every
  piece of static UI text.
- In a `PageModel` (`.cshtml.cs`): inject `IStringLocalizer<SharedResource> localizer` in the
  primary constructor and use `localizer["KeyName"]` anywhere you'd otherwise write a literal
  string into `ModelState.AddModelError(...)`.
- DataAnnotations validation attributes ( `[Required]`, `[EmailAddress]`, `[Compare]`, etc.) get
  localized through the *same* resource: set `ErrorMessage = "FieldRequired"` (a resource key, not
  literal text) on the attribute. `Program.cs` already wires
  `AddDataAnnotationsLocalization(...DataAnnotationLocalizerProvider = ... => factory.Create(typeof(SharedResource))`
  so this resolves through `SharedResource.resx` automatically. Reuse the existing generic keys
  (`FieldRequired`, `EmailInvalid`, `PasswordsDoNotMatch`) across fields/pages instead of minting a
  new key per field — they're field-agnostic on purpose.
- `ViewData["Title"]` must be a plain `string`, so write `L["Login"].Value`, not `L["Login"]`
  (that's a `LocalizedString`, and `_Layout.cshtml` does `ViewData["Title"] as string` — a
  `LocalizedString` isn't a `string` and that cast silently fails to `null`).
- Identity's own generated errors (`IdentityResult.Errors` from `CreateAsync`/`ResetPasswordAsync`
  etc.) are **not** covered by this — they come from the framework's built-in
  `IdentityErrorDescriber` and stay in English until someone overrides that describer. Known gap,
  not silently patched over.
- New pages don't need new plumbing — `_ViewImports.cshtml` already injects `L`, and the culture
  is already resolved by `app.UseRequestLocalization(...)` in `Program.cs` before any page runs.
  Just add your new keys to both `.resx` files and use `L[...]`.

## After changing UI

Razor views compile at build time in this project (`Sdk="Microsoft.NET.Sdk.Web"`), so a broken
`asp-for`/`<partial>`/tag-helper reference is a build error, not just a runtime one:

```
dotnet build src/Administration/Administration.Api/Administration.Api.csproj
```

Run this from `v2/` after any `.cshtml`/`.razor` change. Visual verification (does it actually
look right in a browser) is not something this skill can do — ask the user to check, or use the
`run` skill if you need to drive a browser yourself.
