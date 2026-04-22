# Spec: E2E Test Coverage Gap — Fill Missing Tests

**Status:** 📋 Planned  
**Spec file:** `.github/specs/e2e-test-coverage.md`

---

## Problem Statement

Several completed specs have zero or partial E2E test coverage:

| Spec | Gap |
|---|---|
| `user-authentication.md` | No auth flow tests at all (login, logout, register, ownership, per-user pin/rate) |
| `prompt-description.md` | No E2E tests for description field validation or display |
| `dark-theme.md` | No tests for the theme toggle |
| `initialrequirement.md` | `CreatePromptTests` references stale `#Input_OwnerName` field (removed when auth was added) |

Additionally, existing tests for pin toggle, star rating, create, edit, and delete do not authenticate first, which may cause failures or false passes now that auth is required.

---

## Functional Requirements

### FR-1: Auth helper in `E2ETestBase`
Add a `LoginAsync(string username, string password)` helper that navigates to `/Account/Login`, fills credentials, submits, and waits for the home page. All auth-gated tests will call this.

### FR-2: `AuthenticationTests.cs` (new)
Cover all acceptance criteria from `user-authentication.md`:
- AC-1: Unauthenticated user can browse the full prompt list
- AC-2: Unauthenticated user clicking Create/Edit/Delete/Pin/Rate is redirected to login
- AC-3: Registered user can log in with username + password
- AC-4: Logged-in user can create a prompt; username shown as owner
- AC-5: Edit and Delete buttons only visible to prompt's owner
- AC-6: Direct-URL edit/delete for non-owner returns 403
- AC-7: Pinning is per-user (alice pins; bob does not see it pinned)
- AC-8: Rating a second time shows "Already rated" feedback
- AC-9: Seeded users alice, bob, carol can log in
- AC-10: Logout clears the session

### FR-3: `PromptDescriptionTests.cs` (new)
Cover all E2E acceptance criteria from `prompt-description.md`:
- AC-1: Create without description shows inline validation error
- AC-2: Description > 500 chars shows validation error
- AC-3: Newly created prompt shows description on the home page card
- AC-4: Edit allows updating the description; updated value appears on card
- AC-5: Description appears in full on card (between header and content block)

### FR-4: `DarkThemeTests.cs` (new)
Cover relevant E2E acceptance criteria from `dark-theme.md`:
- AC-2: App loads with `data-bs-theme="dark"` by default
- AC-3: Clicking the theme toggle switches to light mode; `localStorage` stores the choice; reload restores it
- AC-8 (smoke): Core functionality (copy, rate, pin) still works after a theme switch

### FR-5: Fix existing tests
- `CreatePromptTests.cs` — add `LoginAsync` before navigating to Create; remove `#Input_OwnerName` references; add Description field interactions
- `EditPromptTests.cs` — add `LoginAsync` before navigating to Edit
- `DeletePromptTests.cs` — add `LoginAsync` before navigating to Delete
- `PinToggleTests.cs` — add `LoginAsync` before pin interactions
- `StarRatingTests.cs` — add `LoginAsync` before rating interactions

---

## Acceptance Criteria

| # | Criterion |
|---|---|
| AC-1 | All user-authentication.md E2E scenarios are covered |
| AC-2 | All prompt-description.md E2E scenarios are covered |
| AC-3 | Dark theme toggle is covered by E2E tests |
| AC-4 | Existing tests updated to use `LoginAsync` where auth is required |
| AC-5 | `#Input_OwnerName` references removed from tests (field no longer exists) |
| AC-6 | All existing and new tests pass (`dotnet test PromptBank.Tests`) |
