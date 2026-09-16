# Strnadi Design System

This is the single source of truth for how Strnadi UI looks and is built. It covers both
surfaces that share one visual identity:

- **Auth pages** (Razor Pages: `Pages/Account/Login.cshtml`, `Register.cshtml`,
  `ResetPassword.cshtml`, future ones) — minimal chrome, centered card.
- **Admin panel** (Razor Pages under `Pages/Dashboard/`, same as auth pages; a Blazor Server
  rewrite is possible later but not required) — sidebar + topbar shell, data-dense: user tables,
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

  --sidebar-width: 240px;
  --topbar-height: var(--space-10); /* 64px */
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

### Checkbox list — `.ss-checkbox-list` / `.ss-checkbox`

For a stack of standalone checkbox items (currently: required-document consent on Register).
Not a `.ss-field` variant — `.ss-field` is a labeled input wrapper for single-value form fields;
a checkbox list is a set of independent boolean items, each with its own inline label.

```
.ss-checkbox-list
  .ss-checkbox
    input.ss-checkbox__input[type=checkbox]
    label.ss-checkbox__label   -- wraps checkbox text, can contain an inline <a> (uses --color-primary via the global `a` rule)
```

`accent-color: var(--color-primary)` on the input itself (native checkbox styling, no custom
box-drawing needed). Label text `--font-size-sm`, `--color-text`.

### Prose — `.ss-prose`

Long-form rendered content — currently only a document's Markdown content, rendered to HTML
server-side (Markdig) and shown at `GET /documents/{id}/view`. Headings get top margin only (not
before the first child), paragraphs/lists/blockquotes/tables get bottom margin, links use
`--color-primary`, `code` gets a `--color-surface-alt` pill, tables get hairline row borders.
Wrap the rendered HTML in `<article class="ss-card ss-prose">` for a readable single-column page.

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

A card's `<h2>` heading sitting directly above a dense form or list needs `.ss-section-heading`
(`margin-bottom: var(--space-4)`) instead of the default h1-h3 margin (`--space-2`, tuned for body
copy) — otherwise the form crowds the heading. Used on the account page's Profile/Change
password/Documents cards.

### Consent list — `.ss-consent-list` / `.ss-consent-item`

A vertical list of status rows inside a card (document acceptance status and the projects quick
list, both on the account page). Not a `.ss-table` - too few columns and too much per-row content
(title, multiple badges, sometimes an action button) for a table to read well; not a plain
`.ss-stack` either, since rows need a hairline separator and first/last-child padding trimmed
flush with the card edge. The action button is optional per row — the projects list omits it.

```
.ss-consent-list
  .ss-consent-item                  -- flex row, wraps on narrow widths, gap --space-4, hairline border-bottom
    (title/link + .ss-consent-item__badges)
    (action button/form)
```

`.ss-consent-item__badges` stacks badges with `margin-top: var(--space-2)` under the title so they
read as a group, not crammed against it. First item drops top padding, last item drops bottom
padding and its border, so the list sits flush with the card's own padding.

### Stack — `.ss-stack`

`display: flex; flex-direction: column; gap: var(--space-5)`. Use this to space out multiple
`.ss-card`s (or any other blocks) vertically instead of ad-hoc margin on one of them.

### Dashboard grid — `.ss-dashboard-grid`

Two-column layout (`2fr 1fr`, `--space-5` gap) for a page that has one primary block (a form) and
one or more secondary blocks (quick lists, status summaries) that don't need full width — used on
`/dashboard` to avoid stacking every card at the same width down a single column. Collapses to one
column under 768px (same breakpoint the admin shell itself collapses at). Put a `.ss-stack` of
cards in each grid cell rather than a single card per cell, so either column can hold more than
one block.

### Profile header — `.ss-profile-header`

Identity banner shown above the grid on `/dashboard`: an initials avatar plus name/email and a row
of at-a-glance badges (member since, project count, document acceptance status), so the account
page opens with a glanceable summary instead of going straight into a form.

```
.ss-profile-header                 -- flex row, gap --space-4, wraps on narrow widths
  .ss-profile-header__avatar       -- 64px circle, --color-primary bg, initials
  .ss-profile-header__meta
    h1                             -- name
    .ss-profile-header__sub        -- muted, --font-size-sm (email)
    .ss-profile-header__badges     -- flex row of .ss-badge, wraps, margin-top --space-2
```

### Badges — `.ss-badge`

Small pill, `--radius-full`, `--font-size-xs`, `--font-weight-semibold`. Semantic variants same
as alerts (subtle bg + solid text). Used for role names, project status, account status.

### Data table — `<ss-table>`

For the user/project management screens. Implemented as an ASP.NET Core Tag Helper
(`Administration.Api/TagHelpers/SsTableTagHelper.cs`, registered project-wide via
`@addTagHelper *, Administration.Api` in `Pages/_ViewImports.cshtml`), not just a CSS class — every
table page writes a plain `<ss-table><thead>...</thead><tbody>...</tbody></ss-table>` and the tag
helper renders that as `<div class="ss-table-wrapper"><table class="ss-table">...</table></div>`.
This is what all four existing tables (`/dashboard/users`, `/dashboard/projects`,
`/dashboard/documents`, `/dashboard/projects/{id}/roles`) use — a table page should never write
`<table class="ss-table">` or `.ss-table-wrapper` by hand, always go through `<ss-table>`, so the
wrapper fix below and any future shared table behavior lives in one place instead of being
copy-pasted per page.

- Header row: `--color-text-muted`, `--font-size-xs`, uppercase, `--font-weight-semibold`,
  bottom border `--color-border`.
- Body rows: `--font-size-sm`, alternate rows `--color-surface-alt` OR a bottom hairline border
  per row (pick one, don't mix — hairline is the default until a table gets dense enough to need
  striping).
- Row hover: `--color-primary-subtle`.
- Row-level actions render as `.ss-btn--ghost.ss-btn--sm`, right-aligned last column.
- `.ss-table-wrapper` (`overflow-x: auto`, applied automatically by the tag helper) exists because
  a table with enough columns (Users showing the confidential columns is the case that surfaced
  this) needs more width than its card, and without a scroll container it pushes past the card's
  right edge — the last column (usually a row action button) reads as cut off — instead of
  scrolling.
- A page-specific table (e.g. `/dashboard/users`) can extract its `<ss-table>` markup into its own
  partial (`_UsersTable.cshtml`, taking a small view-model record for the rows + the permission
  flags that control which columns/actions show) when the surrounding page has enough else going
  on that inlining the whole table clutters it. That's a per-page organizational choice on top of
  `<ss-table>`, not a substitute for it.
- `create-href`/`create-text` attributes render a `.ss-btn--primary.ss-btn--sm` link top-right,
  above the table (`Pages/Dashboard/Documents/Index.cshtml`, `Pages/Dashboard/Projects/Roles/Index.cshtml`).
  Use these instead of a page hand-building its own title-row button — the default `.ss-btn` is
  `width: 100%` (sized for auth-form submit buttons), and a page-built button that forgets
  `.ss-btn--sm` renders full-width instead of as a normal button; routing every "New X" action
  through `<ss-table>` fixes that in one place. Because the button is part of `<ss-table>`, always
  pass it as an attribute on the same `<ss-table>` element the page always renders (even for an
  empty list) - don't gate the whole `<ss-table>` behind an `if (list.Count == 0)` the way the
  empty-state message used to be, or the create button disappears along with the table when the
  list is empty. Put the empty-state message inside `<tbody>` instead, as a single
  `<tr><td colspan="N">` spanning the header's column count.

#### Table toolbar / search — `.ss-table-toolbar`

`<ss-table>` also renders the search box, not just the create button — like `create-href`, this is
an attribute on `<ss-table>` (`Pages/Dashboard/_UsersTable.cshtml` is the current example), not
markup a page writes by hand:

- `search-name` / `search-value` / `search-placeholder` / `search-button-text` — a plain search
  input + submit button. Omitting `search-name` renders no search UI at all (Projects/Documents/
  Roles don't have one).
- `search-field-name` / `search-field-value` / `search-fields` — an optional `<select>` next to
  the input, letting the caller pick which column to search (`/dashboard/users` offers "Name" and,
  only for `CanViewUsersConfidential`, "Email"). `search-fields` takes a
  `IReadOnlyList<SsTableSearchField>` (a `(Value, Text)` record from
  `Administration.Api.TagHelpers`) built by the page/partial; omit `search-field-name` for a plain
  single-box search with no column picker. The page's `OnGetAsync` is what actually branches on
  the selected field when building its EF query — the tag helper only renders the picker and
  round-trips the bound values.

All of it renders as one `method="get"` form inside `.ss-table-toolbar` (`display: flex;
justify-content: space-between`, wraps on narrow widths) so the search term/field are bookmarkable
query-string params with no JS, and the page model filters server-side. The search input itself is
`.ss-field__input.ss-table-toolbar__search` (caps it at `max-width: 280px` instead of the field
input's usual full width); the column `<select>` is `.ss-field__input.ss-table-search__field`
(`width: auto`, same reasoning as the create button - the default `.ss-field__input` is full
width). When both a search form and a create button are configured, the search form takes the
toolbar's left side and the create button the right (`.ss-table-toolbar--end` only applies when
there's a create button and no search form, so a lone item doesn't collapse to the left).

Like the create button, the search form is part of `<ss-table>` itself, so it always renders even
when the filtered result set is empty — the empty-state message goes inside `<tbody>` as a
spanning `<tr><td colspan="N">` row (see `_UsersTable.cshtml`), not as a sibling that replaces the
whole table, or the search box would disappear exactly when it's needed to change the query.

### Navigation — `.ss-sidebar` / `.ss-topbar`

Admin panel shell only (not used on auth pages). Dimensions come from two dedicated tokens,
`--sidebar-width` (240px) and `--topbar-height` (`var(--space-10)`, 64px) — not the small spacing
scale, since these are fixed layout dimensions, not gaps/padding.

- `.ss-sidebar`: fixed left, full height, `--color-primary` background. `.ss-sidebar__brand` is
  the wordmark at the top; `.ss-sidebar__nav` stacks `.ss-sidebar__link` items, which get
  `--color-accent` left border (3px) + `rgba(255,255,255,0.08)` background when
  `.ss-sidebar__link--active`. Under 768px it becomes a static, horizontally-scrolling strip
  instead of a fixed column (link active-state switches to a bottom border in that mode).
- `.ss-topbar`: fixed top, starting after the sidebar, `--color-surface` background, bottom
  border. `.ss-topbar__title` on the left (page title), `.ss-topbar__user` on the right — current
  user's name, a "My account" link, and the logout button. This is also the entry point for a
  non-admin user managing their own profile — same shell, just fewer sidebar links.
- `.ss-admin-content` offsets by `--sidebar-width`/`--topbar-height` (0 under 768px, sidebar and
  topbar go static there) and holds `.ss-admin-content__inner` (`max-width: 1440px`, `--space-6`
  padding) for the actual page content.

### Language switch — `.ss-lang-switch`

Fixed top-right corner. Used on the auth shell (rendered unconditionally by `_Layout.cshtml`).
`.ss-lang-switch__item` is a plain text link (`--font-size-xs`, `--font-weight-semibold`,
`--color-text-muted`); `.ss-lang-switch__item--active` marks the current UI culture
(`--color-primary` text on `--color-primary-subtle` background). Links point at
`GET /culture/set?culture={cs|en}&returnUrl=...`, which sets the culture cookie and redirects
back — see the Localization section of the `strnadi-ui` skill (`.claude/skills/strnadi-ui/SKILL.md`)
for how UI text is localized.

On the admin panel shell, the same links render inline inside `.ss-topbar__user` via
`.ss-topbar__langs` (`display: flex; gap: var(--space-2)`) instead of the fixed `.ss-lang-switch`
wrapper — the fixed corner position would sit on top of the topbar's user cluster, which also
anchors top-right. `_DashboardLayout.cshtml` sets `ViewData["HideLangSwitch"] = true` so
`_Layout.cshtml` skips its own fixed widget when nested inside the dashboard shell; the anchor
markup and `.ss-lang-switch__item`/`.ss-lang-switch__item--active` classes are reused as-is,
only the positioning wrapper differs.

## Page layouts

### Auth layout (Login/Register/ResetPassword)

Single `.ss-card.ss-auth-card` (max-width 420px), centered both axes on `--color-bg`, inside
`.ss-auth-shell`. No sidebar, no topbar. Wordmark above the card (`.ss-auth-wordmark`).

Forms with more than ~3 fields (Register) use `.ss-auth-card--wide` (max-width 560px) plus
`.ss-field-row` to pair fields — a plain stack of 5+ full-width fields at 420px reads as an
overlong column that needs scrolling to reach the submit button; widening + pairing keeps the
whole card visible without shrinking type or spacing tokens to compensate.

### Admin panel layout

`.ss-sidebar` fixed left, `.ss-topbar` fixed top spanning the remaining width, `.ss-admin-content`
offset to clear both and scrolling independently. A user who is not an admin sees the same shell
with a reduced sidebar (just "My account", no "Users"). Project-scoped sections
(`/dashboard/projects/{id}`) aren't picked via a cookie claim — a project is just a page you
navigate to from `/dashboard/projects`, and that page checks the caller's role against that
specific project fresh on every request.

## Implementation status

Done: `Administration.Api/wwwroot/design-system.css` exists with all tokens and components above.
`Pages/_Layout.cshtml` loads it for every Razor Page, and renders `.ss-lang-switch`. `Login.cshtml`,
`Register.cshtml`, and `ResetPassword.cshtml` are built on it, including branded Google/Apple
buttons (`Pages/Account/_ExternalProviders.cshtml`) wired to the existing external-login endpoints,
and are fully localized (cs default, en, de) — see the `strnadi-ui` skill for the localization pattern.
The admin panel shell (`Pages/Dashboard/_DashboardLayout.cshtml`) is built on the same CSS and
nests inside `_Layout.cshtml`, with its own inline language switch in the topbar (see § Language
switch); `/dashboard` (own profile) and `/dashboard/projects[/{id}]` exist as role-gated Razor
Pages, with Projects management still placeholder content pending its own build-out.
`/dashboard/users` and `/dashboard/users/{id}/edit` are built out: the list is gated on
`ViewUsersBasic`/`ManageUsers` (basic columns: name, registration date, status) with
`ViewUsersConfidential`/`ManageUsers` additionally showing email/city/postal code, and the edit
page (gated on `ManageUsers` only) lets an admin change any of a user's own fields plus set a new
password (`NewPassword`/`ConfirmNewPassword` — set-only, the current password is never shown or
requested back).
`/dashboard/documents`, `/dashboard/documents/create`, and `/dashboard/documents/{id}/edit` are
built out and gated on `ManageDocuments`: the list shows active documents with type/project/version/
effective date/required badge; create makes version 1 of a platform-wide (non-project-scoped)
document; edit doesn't mutate the row in place — it publishes a new version (deactivates the
current one, increments `Version`) and emails everyone whose acceptance of the previous version no
longer covers the new one (`IDocumentEmailSender`, same as the API's `PUT /documents/{id}`).
Project-scoped document management is still API-only.

Not done yet:

1. Projects management screen is still a placeholder (`.ss-card` with a "coming soon" message) —
   the shell/nav/access-control around it is real, the CRUD UI inside isn't.
2. No `CLAUDE.md` entry points at this file yet; for now UI work in this repo is covered by the
   `strnadi-ui` Claude Code skill (`.claude/skills/strnadi-ui/SKILL.md`) instead.
3. A Blazor Server rewrite of the admin panel remains optional future work, not a requirement —
   if it happens, it needs to load the same `design-system.css`, one `<link>`, not a per-surface
   copy.
