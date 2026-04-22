# Spec: Single-Column Prompt Card Layout

## Summary
Change prompt cards from a multi-column responsive grid (1→2→3 columns) to a single full-width column at all screen sizes, growing vertically.

## Background
Currently `Index.cshtml` uses Bootstrap's `row-cols-1 row-cols-md-2 row-cols-lg-3` which shows 3 cards per row on large screens. The desired UX is one card per row so users can scan and read prompts without horizontal crowding.

## Acceptance Criteria
- [ ] Prompt cards always render one per row at all viewport sizes (mobile, tablet, desktop)
- [ ] Cards make good use of the full horizontal width (no wasted space)
- [ ] Card internals are polished for full-width layout (metadata, buttons, content area)
- [ ] Dark and light themes both look correct
- [ ] No regression in existing functionality (copy, pin, rate, edit, delete)

## Files to Change
- `PromptBank/Pages/Index.cshtml` — grid wrapper class + card layout improvements
- `PromptBank/wwwroot/css/site.css` — CSS tweaks for full-width cards if needed
