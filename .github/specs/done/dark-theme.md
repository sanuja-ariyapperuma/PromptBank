# Dark Theme — Professional Tech UI

## Problem

The current UI uses a fixed light colour palette with hard-coded colour values (`#4f46e5`, `#eef2ff`, `#f8f7ff`, etc.) scattered across inline `style=""` attributes in every `.cshtml` page and an inline `<style>` block in `_Layout.cshtml`. There is no dark mode, which is expected by modern developer/technical audiences.

## Goal

Restyle PromptBank with a professional dark tech aesthetic (think GitHub/VS Code dark) while keeping the existing indigo accent colour family. The result should feel like a polished internal developer tool.

---

## Visual Design Direction

| Token                | Light value        | Dark value        |
|----------------------|--------------------|-------------------|
| `--pb-bg`            | `#f8f7ff`          | `#0d1117`         |
| `--pb-surface`       | `#ffffff`          | `#161b22`         |
| `--pb-surface-raised`| `#eef2ff`          | `#1e293b`         |
| `--pb-border`        | `#e0e7ff`          | `#30363d`         |
| `--pb-text`          | `#1e1b4b`          | `#e6edf3`         |
| `--pb-text-muted`    | `#6b7280`          | `#8b949e`         |
| `--pb-primary`       | `#4f46e5`          | `#6366f1`         |
| `--pb-primary-hover` | `#4338ca`          | `#818cf8`         |
| `--pb-code-bg`       | `#f1f5f9`          | `#161b22`         |

Navbar: dark glass look — semi-transparent dark background with a subtle indigo gradient glow/border.

---

## Acceptance Criteria

### AC-1 — CSS Custom Properties
- All colours are expressed via CSS custom properties (`--pb-*`) defined in `site.css`.
- No raw colour hex values appear as inline `style=""` attributes in `.cshtml` files; every element uses a CSS class instead.

### AC-2 — Dark Mode Default
- The app loads in dark mode by default (`data-bs-theme="dark"` on `<html>`).
- Bootstrap 5 dark mode (`data-bs-theme`) is used so native Bootstrap components (forms, alerts, badges) adopt dark styles automatically.

### AC-3 — Theme Toggle
- A sun/moon icon toggle button appears in the navbar (far right, before auth links).
- Clicking it switches between light and dark themes and persists the choice in `localStorage`.
- On page load, the stored preference (if any) is applied before first paint to prevent flash.
- Accessible: `aria-label` updates dynamically ("Switch to light mode" / "Switch to dark mode").

### AC-4 — Prompt Cards
- Cards have a dark background (`--pb-surface`) with a subtle `--pb-border` border.
- Card header uses `--pb-surface-raised`.
- The `prompt-content` `<pre>` block uses `--pb-code-bg` with a monospace font and correct contrast.
- Pin badge is clearly visible in dark mode.

### AC-5 — Forms & Auth Pages
- `Create`, `Edit`, `Login`, `Register`, `Delete` pages use CSS classes (not inline hex colours).
- Form controls, labels, and validation messages have correct contrast in both themes.

### AC-6 — Stars & Interactions
- Filled stars remain amber (`#f59e0b`) in both themes.
- Empty stars use `--pb-border` colour so they are visible but subdued.
- Hover/focus states are visible.

### AC-7 — Footer & Misc
- Footer adopts dark background using `--pb-surface-raised`.
- No white flash on initial load in dark mode.

### AC-8 — No Regressions
- All existing functionality (copy, rating, pin, auth) continues to work.
- Existing unit tests and E2E tests pass unchanged.

---

## Files to Change

| File | Change |
|------|--------|
| `wwwroot/css/site.css` | Add all CSS custom properties, dark/light theme variables, component classes |
| `Pages/Shared/_Layout.cshtml` | Remove inline `<style>` block; add `data-bs-theme` attr; add toggle button; add anti-flash script |
| `Pages/Index.cshtml` | Replace inline `style=""` hex colours with CSS utility classes |
| `Pages/Prompts/Create.cshtml` | Same |
| `Pages/Prompts/Edit.cshtml` | Same |
| `Pages/Prompts/Delete.cshtml` | Same |
| `Pages/Account/Login.cshtml` | Same |
| `Pages/Account/Register.cshtml` | Same |
| `wwwroot/js/site.js` | Add theme toggle logic (localStorage read/write, DOM class toggle) |

---

## Out of Scope

- Per-user theme preference persisted server-side.
- Custom fonts or icon changes.
- Changes to business logic, data model, or back-end code.
