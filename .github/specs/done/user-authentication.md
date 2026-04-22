# Spec: User Authentication & Per-User Personalisation

**Status:** 📋 Planned  
**Spec file:** `.github/specs/user-authentication.md`

---

## Problem Statement

The app currently has no concept of identity — anyone can edit or delete any prompt, and pinning is global. This spec introduces ASP.NET Core Identity so users own their prompts, can personalise their pin list, and cannot vote more than once per prompt.

---

## Functional Requirements

### FR-1: Registration
- A public `/Account/Register` page allows anyone to create an account.
- Required fields: **Username** (unique), **Password**, **Confirm Password**.
- On success, the user is automatically signed in and redirected to the home page.
- Standard ASP.NET Core Identity password rules apply (configurable).

### FR-2: Login / Logout
- A `/Account/Login` page accepts **Username** and **Password**.
- Failed login shows a generic error: *"Invalid username or password."*
- A **Logout** link in the nav bar signs the user out and redirects to the home page.
- Login state is maintained via an authentication cookie.

### FR-3: Browse Without Login (Read-Only)
- Unauthenticated users can browse the prompt list and read all prompts.
- The following actions require login — clicking them redirects to `/Account/Login`:
  - Create a prompt
  - Edit a prompt
  - Delete a prompt
  - Pin / Unpin a prompt
  - Rate a prompt

### FR-4: Prompt Ownership
- When a logged-in user creates a prompt, the prompt is linked to their account (`OwnerId` FK).
- The `OwnerName` display field is replaced by the user's **Username**.
- Only the prompt's owner can see the **Edit** and **Delete** buttons on a card.
- Attempting to `POST` an edit or delete for a prompt the user doesn't own returns `403 Forbidden`.

### FR-5: Per-User Pinning
- Pinning is no longer a global flag on the `Prompt` entity.
- Each user maintains their own pin list stored in a `UserPromptPin` join table (`UserId`, `PromptId`).
- On the home page, a logged-in user sees their own pinned prompts at the top.
- Unauthenticated users see no pinned prompts (prompts are ordered by rating then date only).
- The pin badge (📌) on a card is only visible to the user who pinned it.

### FR-6: Per-User Rating (One Vote Per Prompt)
- Each user may vote on a prompt **at most once**.
- Votes are stored in a `UserPromptRating` join table (`UserId`, `PromptId`, `Stars`).
- Attempting to vote a second time on the same prompt returns a `409 Conflict`; the UI shows a subtle "Already rated" message on the star row.
- `Prompt.RatingTotal` and `Prompt.RatingCount` continue to hold the aggregate (updated on each new vote); they are not rolled back if a user changes their mind (votes are immutable once cast).
- For unauthenticated users, the star row is visible but clicking redirects to login.

### FR-7: Navigation Bar
- Show **Register** and **Login** links when unauthenticated.
- Show **Username** (non-clickable or profile link) and **Logout** when authenticated.

### FR-8: Seed Data (Replacement)
- Remove all existing seed prompts and seed users.
- Seed **3 users** with known credentials for development and demo:

| Username | Password |
|---|---|
| `alice` | `Alice@1234` |
| `bob` | `Bob@1234` |
| `carol` | `Carol@1234` |

- Seed **5 prompts** owned by the seeded users:

| Title | Owner | Pinned by |
|---|---|---|
| Explain code | alice | alice |
| Write unit tests | bob | bob |
| Code review checklist | carol | — |
| Generate SQL query | alice | — |
| Summarise PR | bob | — |

---

## Non-Functional Requirements

### NFR-1: ASP.NET Core Identity
- Use `Microsoft.AspNetCore.Identity.EntityFrameworkCore`.
- `ApplicationUser` extends `IdentityUser` (no extra profile fields required in this spec).
- `AppDbContext` extends `IdentityDbContext<ApplicationUser>`.

### NFR-2: No Third-Party Auth
- No OAuth / social login providers in this spec. Local accounts only.

### NFR-3: No Role-Based Authorization
- No admin/moderator roles. All authenticated users have equal permissions (limited to their own prompts for edit/delete).

### NFR-4: Cookie Authentication
- Use the default ASP.NET Core Identity cookie middleware.
- Cookie expiry: sliding 7-day session.

### NFR-5: Antiforgery
- All POST forms continue to use the built-in antiforgery token (already enabled by default in Razor Pages).

---

## Data Model Changes

### New: `ApplicationUser`
```csharp
public class ApplicationUser : IdentityUser
{
    // Extend here in future specs if profile fields are needed
}
```

### Modified: `Prompt`
```csharp
public class Prompt
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; }

    [Required, MaxLength(4000)]
    public string Content { get; set; }

    // OwnerId replaces the plain-text OwnerName field
    public string OwnerId { get; set; }                  // FK → ApplicationUser.Id
    public ApplicationUser Owner { get; set; }           // navigation property

    public bool IsPinned { get; set; }                   // REMOVED — replaced by UserPromptPin

    public int RatingTotal { get; set; }
    public int RatingCount { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation
    public ICollection<UserPromptPin> Pins { get; set; }
    public ICollection<UserPromptRating> Ratings { get; set; }
}
```

### New: `UserPromptPin`
```csharp
public class UserPromptPin
{
    public string UserId { get; set; }       // FK → ApplicationUser.Id
    public int PromptId { get; set; }        // FK → Prompt.Id

    public ApplicationUser User { get; set; }
    public Prompt Prompt { get; set; }
}
// Composite PK: (UserId, PromptId)
```

### New: `UserPromptRating`
```csharp
public class UserPromptRating
{
    public string UserId { get; set; }       // FK → ApplicationUser.Id
    public int PromptId { get; set; }        // FK → Prompt.Id
    public int Stars { get; set; }           // 1–5

    public ApplicationUser User { get; set; }
    public Prompt Prompt { get; set; }
}
// Composite PK: (UserId, PromptId)
```

---

## Service Layer Changes

### `IPromptService` additions
```csharp
// Sorting now requires the current userId to resolve per-user pins
Task<IReadOnlyList<Prompt>> GetAllAsync(string? userId, CancellationToken ct);

// Returns null if user has already voted
Task<RatingResult?> RateAsync(int promptId, string userId, int stars, CancellationToken ct);

Task TogglePinAsync(int promptId, string userId, CancellationToken ct);

// Ownership check built-in; throws UnauthorizedAccessException if userId != owner
Task UpdateAsync(int id, string userId, string title, string content, CancellationToken ct);
Task DeleteAsync(int id, string userId, CancellationToken ct);
```

---

## Page / Handler Changes

| Page | Change |
|---|---|
| `Index` | Pass `userId` from `User.FindFirstValue(ClaimTypes.NameIdentifier)` to `GetAllAsync` |
| `Prompts/Create` | Require `[Authorize]`; set `OwnerId = currentUserId` |
| `Prompts/Edit` | Require `[Authorize]`; pass `userId` to `UpdateAsync` for ownership check |
| `Prompts/Delete` | Require `[Authorize]`; pass `userId` to `DeleteAsync` for ownership check |
| `Index.OnPostRateAsync` | Require auth; pass `userId`; return `409` if already rated |
| `Index.OnPostTogglePinAsync` | Require auth; pass `userId` |
| `Account/Register` *(new)* | Public; scaffold via Identity or hand-written Razor Page |
| `Account/Login` *(new)* | Public; scaffold via Identity or hand-written Razor Page |
| `Account/Logout` *(new)* | POST handler; signs out and redirects |

---

## UI Changes

| Element | Change |
|---|---|
| Prompt card — Edit/Delete buttons | Hidden from users who are not the prompt's owner |
| Prompt card — Pin button | Hidden from unauthenticated users |
| Prompt card — Star row | Visible to all; clicking redirects unauthenticated users to login; stars are disabled after voting |
| Prompt card — Owner label | Display `prompt.Owner.UserName` instead of the old `OwnerName` text field |
| Nav bar | Add Register/Login (unauthenticated) or Username/Logout (authenticated) |

---

## Migration Strategy

1. Add `Microsoft.AspNetCore.Identity.EntityFrameworkCore` NuGet package.
2. Change `AppDbContext` to extend `IdentityDbContext<ApplicationUser>`.
3. Add `UserPromptPin` and `UserPromptRating` entities.
4. Remove `Prompt.IsPinned` and `Prompt.OwnerName`; add `Prompt.OwnerId`.
5. Run `dotnet ef migrations add AddIdentityAndPersonalisation`.
6. Update seed data: create seeded users via `UserManager` in a startup seed method (not via `HasData`, as Identity hashes passwords at runtime).
7. Delete old seed prompts; add new ones linked to seeded user IDs.

---

## Acceptance Criteria

| # | Criterion |
|---|---|
| AC-1 | Unauthenticated user can browse the full prompt list |
| AC-2 | Unauthenticated user clicking Create/Edit/Delete/Pin/Rate is redirected to Login |
| AC-3 | Registered user can log in with username + password |
| AC-4 | Logged-in user can create a prompt; it shows their username as owner |
| AC-5 | Edit and Delete buttons are only visible to the prompt's owner |
| AC-6 | Attempting to edit/delete another user's prompt via direct URL returns 403 |
| AC-7 | Pinning a prompt only pins it for the current user; other users do not see it pinned |
| AC-8 | Rating a prompt a second time returns "Already rated" feedback; aggregate is unchanged |
| AC-9 | Seeded users alice, bob, carol can log in with their seeded passwords |
| AC-10 | Logout clears the session and returns to the browse view |
