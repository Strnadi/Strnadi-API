# Strnadi Design System

This is the single source of truth for how Strnadi UI looks and is built. It covers both
surfaces that share one visual identity:

- **Auth pages** (Razor Pages: `Pages/Account/Login.cshtml`, `Register.cshtml`,
  `ResetPassword.cshtml`, future ones) — minimal chrome, centered card.
- **Admin panel** (future Blazor Server app) — sidebar + topbar shell, data-dense: user tables,
  project tables, role management.
- Anything a regular (non-admin) user sees when managing their own account also lives on the
  Admin panel shell, just scoped to what that user is allowed to touch — same components, same
  tokens, different menu/permissions. There is no separate "user app" visual language.

## How to use this doc (rules for Claude Code / anyone writing UI here)

1. **Read this file before writing or editing any `.cshtml`, `.razor`, or CSS.**
2. Never hardcode a color, spacing value, font size, or radius in markup or component CSS —
   always reference the CSS custom property from `wwwroot/design-system.css` (§ Tokens).
3. Before inventing a new component pattern, check § Components first. Reuse an existing class.
   If nothing fits, add the new pattern to this doc in the same change, don't leave it undocumented.
4. Auth pages and the admin panel both load the same `design-system.css` — do not fork it
   per-surface. Page-specific layout (centered card vs. sidebar shell) is the only thing that
   differs; see § Page Layouts.
5. If a change here would alter an existing token's value (not just add a new one), flag it
   explicitly instead of changing it silently — it likely affects every page at once.

## Brand direction

"Strnadi" (yellowhammer/bunting) is a bioacoustic citizen-science project: people record and
classify bird dialects. The UI should read as a **field journal crossed with a data tool** —
calm, precise, a little warm — not a generic SaaS dashboard and not playful/childish. Primary
color is a deep forest green (habitat, trust, works for a lot of chrome); accent is a warm
amber-gold (a nod to the yellowhammer's plumage), used sparingly for emphasis, never as a
background for large areas.

## Tokens

All tokens are CSS custom properties on `:root`, defined once in `wwwroot/design-system.css`.

### Color

```css
:root {
  /* Surfaces */
  --color-bg: #FAF8F3;          /* page background, warm paper */
  --color-surface: #FFFFFF;      /* cards, inputs, modals */
  --color-surface-alt: #F1EDE3;  /* table row stripes, subtle panels */
  --color-border: #E3DDCB;

  /* Text */
  --color-text: #211F1A;
  --color-text-muted: #6B6558;
  --color-text-on-primary: #FFFFFF;
  --color-text-on-accent: #211F1A; /* gold is too light for white text */

  /* Brand */
  --color-primary: #1F4D3D;
  --color-primary-hover: #17392D;
  --color-primary-subtle: #E7EFEA;  /* tinted backgrounds, e.g. active nav item */
  --color-accent: #D4930A;
  --color-accent-hover: #B87C08;

  /* Semantic */
  --color-success: #2F7D4F;
  --color-success-subtle: #E6F3EA;
  --color-warning: #C2660C;
  --color-warning-subtle: #FBEEDD;
  --color-danger: #B3261E;
  --color-danger-subtle: #FBE9E8;
  --color-info: #2563A8;
  --color-info-subtle: #E8F0F9;
}
```

Every `-subtle` color is a light tint of its parent, used as a background behind text/icons of
that same semantic color (badges, alert banners, validation summaries) — never full-saturation
color as a large background.

### Typography

One font family everywhere — auth pages and admin panel. Load **Inter** (system stack fallback);
it's legible at small sizes, which the admin panel's data tables need.

```css
:root {
  --font-family: "Inter", system-ui, -apple-system, "Segoe UI", sans-serif;

  --font-size-xs: 0.75rem;   /* 12px — table meta, timestamps */
  --font-size-sm: 0.875rem;  /* 14px — table body, form hints */
  --font-size-base: 1rem;    /* 16px — body text, inputs */
  --font-size-lg: 1.125rem;  /* 18px — card titles */
  --font-size-xl: 1.375rem;  /* 22px — section headers */
  --font-size-2xl: 1.75rem;  /* 28px — page titles */
  --font-size-3xl: 2.25rem;  /* 36px — auth page headline only */

  --font-weight-regular: 400;
  --font-weight-medium: 500;
  --font-weight-semibold: 600;

  --line-height-tight: 1.25;
  --line-height-normal: 1.5;
}
```

### Spacing

4px base unit. Use these everywhere instead of ad-hoc `margin`/`padding` values.

```css
:root {
  --space-1: 4px;
  --space-2: 8px;
  --space-3: 12px;
  --space-4: 16px;
  --space-5: 24px;
  --space-6: 32px;
  --space-8: 48px;
  --space-10: 64px;
}
```

### Radius & shadow

```css
:root {
  --radius-sm: 4px;   /* inputs, badges */
  --radius-md: 8px;   /* buttons, cards */
  --radius-lg: 14px;  /* auth card, modals */
  --radius-full: 999px; /* pills, avatars */

  --shadow-sm: 0 1px 2px rgba(33, 31, 26, 0.06);
  --shadow-md: 0 4px 12px rgba(33, 31, 26, 0.10);
  --shadow-lg: 0 12px 32px rgba(33, 31, 26, 0.14); /* modals, dropdowns */
}
```

## Components

Class names below are a plain-CSS naming convention (`.ss-*` = Strnadi System), framework
agnostic — the same classes work in a `.cshtml` form and a `.razor` component.

### Buttons — `.ss-btn`

- `.ss-btn.ss-btn--primary` — solid `--color-primary`, white text. Main call to action per page
  (Login submit, Register submit, "Create project", "Save").
- `.ss-btn.ss-btn--secondary` — outlined, `--color-border` border, `--color-text` text. Secondary
  actions ("Cancel").
- `.ss-btn.ss-btn--ghost` — no border/background until hover (`--color-surface-alt`). Row-level
  actions in tables (e.g. "Edit").
- `.ss-btn.ss-btn--danger` — solid `--color-danger`. Destructive actions ("Delete user"), always
  behind a confirmation.
- Sizes: default and `.ss-btn--sm` (table rows, toolbars). Radius `--radius-md`, padding
  `--space-2` / `--space-4`, font-weight `--font-weight-medium`.

### Form fields — `.ss-field`

Wraps label + input + hint/error, replaces the raw `<div>`/`asp-validation-for` markup currently
in `Login.cshtml`/`Register.cshtml`.

```
.ss-field
  .ss-field__label
  .ss-field__input      (or select/textarea)
  .ss-field__hint        muted, --font-size-sm
  .ss-field__error       --color-danger, --font-size-sm, shown when .ss-field--invalid
```

Input default border `--color-border`; focus ring `--color-accent` (2px outline) — this is the
one place accent color is used as a border, not just for small highlights. Invalid state: border
`--color-danger`.

`.ss-field-row` pairs two `.ss-field` side by side (flex, `--space-4` gap, collapses to a single
column under 600px, before the paired inputs get cramped) — use it to shorten a tall form instead of stacking every field full-width
(e.g. first/last name, password/confirm on Register). Don't pair fields that need the full width
for their content or error text (email, anything with a long validation message).

### Alerts / validation summary — `.ss-alert`

Replaces the bare `validation-summary-errors` div. Variants `--success` / `--warning` /
`--danger` / `--info`, each background `--color-*-subtle`, text/icon `--color-*`, left border
4px solid `--color-*`, radius `--radius-md`, padding `--space-3` `--space-4`.

Always use `asp-validation-summary="ModelOnly"`, never `"All"`. `"All"` repeats every
field-level error (already shown under its own `.ss-field__error`) a second time in this summary
— on a form with several fields that duplication is what makes the card grow and push the submit
button below the fold when validation fails. `ModelOnly` only surfaces errors added with no field
key (`ModelState.AddModelError(string.Empty, ...)`, e.g. "Invalid login attempt.", Identity
`CreateAsync` errors) — field-specific errors stay exactly where they already are, inline.

### Cards — `.ss-card`

`--color-surface` background, `--radius-lg`, `--shadow-sm` at rest, border `--color-border`.
Used for the auth card and for panel sections in the admin UI (e.g. a project's settings block).

### Badges — `.ss-badge`

Small pill, `--radius-full`, `--font-size-xs`, `--font-weight-semibold`. Semantic variants same
as alerts (subtle bg + solid text). Used for role names, project status, account status.

### Data table — `.ss-table`

For the user/project management screens.

- Header row: `--color-text-muted`, `--font-size-xs`, uppercase, `--font-weight-semibold`,
  bottom border `--color-border`.
- Body rows: `--font-size-sm`, alternate rows `--color-surface-alt` OR a bottom hairline border
  per row (pick one, don't mix — hairline is the default until a table gets dense enough to need
  striping).
- Row hover: `--color-primary-subtle`.
- Row-level actions render as `.ss-btn--ghost.ss-btn--sm`, right-aligned last column.

### Navigation — `.ss-sidebar` / `.ss-topbar`

Admin panel shell only (not used on auth pages).

- `.ss-sidebar`: fixed width (240px), `--color-primary` background, `--color-text-on-primary`
  text, items get `--color-accent` left border (3px) + `background: rgba(255,255,255,0.08)` when
  active.
- `.ss-topbar`: `--color-surface` background, bottom border `--color-border`, holds page title +
  current-user menu (avatar, name, "My account" / "Log out" — this is also the entry point for a
  non-admin user managing their own profile).

### Language switch — `.ss-lang-switch`

Fixed top-right corner, works on any layout (auth shell today, admin panel shell later).
`.ss-lang-switch__item` is a plain text link (`--font-size-xs`, `--font-weight-semibold`,
`--color-text-muted`); `.ss-lang-switch__item--active` marks the current UI culture
(`--color-primary` text on `--color-primary-subtle` background). Links point at
`GET /culture/set?culture={cs|en}&returnUrl=...`, which sets the culture cookie and redirects
back — see the Localization section of the `strnadi-ui` skill (`.claude/skills/strnadi-ui/SKILL.md`)
for how UI text is localized.

## Page layouts

### Auth layout (Login/Register/ResetPassword)

Single `.ss-card.ss-auth-card` (max-width 420px), centered both axes on `--color-bg`, inside
`.ss-auth-shell`. No sidebar, no topbar. Wordmark above the card (`.ss-auth-wordmark`).

Forms with more than ~3 fields (Register) use `.ss-auth-card--wide` (max-width 560px) plus
`.ss-field-row` to pair fields — a plain stack of 5+ full-width fields at 420px reads as an
overlong column that needs scrolling to reach the submit button; widening + pairing keeps the
whole card visible without shrinking type or spacing tokens to compensate.

### Admin panel layout

`.ss-sidebar` fixed left, `.ss-topbar` fixed top spanning the remaining width, content area
scrolls independently with `--space-6` padding, `--color-bg` background. A user who is not an
admin sees the same shell with a reduced sidebar (just "My account", no "Users"/"Projects").

## Implementation status

Done: `Administration.Api/wwwroot/design-system.css` exists with all tokens and components above.
`Pages/_Layout.cshtml` loads it for every Razor Page, and renders `.ss-lang-switch`. `Login.cshtml`,
`Register.cshtml`, and `ResetPassword.cshtml` are built on it, including branded Google/Apple
buttons (`Pages/Account/_ExternalProviders.cshtml`) wired to the existing external-login endpoints,
and are fully localized (cs default, en) — see the `strnadi-ui` skill for the localization pattern.

Not done yet:

1. The future Blazor admin `MainLayout.razor` needs to load the same `design-system.css` — one
   `<link>`, not a per-surface copy.
2. `.ss-sidebar` / `.ss-topbar` and the admin panel shell itself don't exist yet — only designed
   on paper (§ Admin panel layout).
3. No `CLAUDE.md` entry points at this file yet; for now UI work in this repo is covered by the
   `strnadi-ui` Claude Code skill (`.claude/skills/strnadi-ui/SKILL.md`) instead.
