---
name: check-conventions
description: >
  Use this skill to audit code against PromptBank's project conventions before committing
  or during code review. Covers XML docs, service layer separation, validation, Bootstrap
  layout, sorting, handler naming, and data model rules.
---

# PromptBank Convention Checklist

Use this skill to audit new or changed code. Work through each section that applies
to the files being reviewed. Raise each violation as a concrete fix, not just a flag.

---

## 1. XML Documentation Comments

**Rule:** Every `public` class, method, and property **must** have XML doc comments.
Minimum required tags:
- `<summary>` on all public members
- `<param name="...">` for every method parameter
- `<returns>` for every method with a non-void return type
- `<exception cref="...">` for every documented thrown exception

```csharp
// ✅ Correct
/// <summary>
/// Toggles the pinned state of a prompt for a specific user.
/// </summary>
/// <param name="promptId">The primary key of the prompt to pin or unpin.</param>
/// <param name="userId">The ID of the user toggling the pin.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns><c>true</c> if now pinned; <c>false</c> if now unpinned.</returns>
public async Task<bool> TogglePinAsync(int promptId, string userId, CancellationToken ct = default)

// ❌ Missing docs — must be fixed
public async Task<bool> TogglePinAsync(int promptId, string userId, CancellationToken ct = default)
```

**Where to check:** All files under `PromptBank/Models/`, `PromptBank/Services/`, `PromptBank/Data/`,
and `PromptBank/Pages/`.

---

## 2. Service Layer Separation (Thin Page Models)

**Rule:** Page models must **not** contain query logic or business logic.
All database access and business rules live in `PromptService` (injected as `IPromptService`).

```csharp
// ✅ Correct — page model delegates to service
public async Task OnGetAsync()
{
    CurrentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
    Prompts = await _promptService.GetAllAsync(CurrentUserId);
}

// ❌ Wrong — page model queries the DB directly
public async Task OnGetAsync()
{
    Prompts = await _db.Prompts.OrderByDescending(p => p.CreatedAt).ToListAsync();
}
```

**Where to check:** All `.cshtml.cs` files under `PromptBank/Pages/`.

New service methods belong in `PromptBank/Services/PromptService.cs` **and** must be added
to `PromptBank/Services/IPromptService.cs` with full XML docs.

---

## 3. Validation — DataAnnotations Only

**Rule:** Use `[Required]`, `[MaxLength]`, `[Range]` from `System.ComponentModel.DataAnnotations`.
Do **not** introduce FluentValidation or any third-party validation library.

Current model constraints to verify against:
| Property | Constraints |
|---|---|
| `Prompt.Title` | `[Required]`, `[MaxLength(200)]` |
| `Prompt.Description` | `[Required]`, `[MaxLength(500)]` |
| `Prompt.Content` | `[Required]`, `[MaxLength(4000)]` |
| `Prompt.OwnerId` | `[Required]` |
| `RateRequest.Stars` | `[Range(1, 5)]` |

**Where to check:** `PromptBank/Models/Prompt.cs` and any new model or request record.

---

## 4. Sorting Order

**Rule:** Prompts are **always** sorted: **pinned first → average rating descending → CreatedAt descending**.

```csharp
// ✅ Correct sort (with userId for per-user pins)
.OrderByDescending(p => pinnedIds.Contains(p.Id))
.ThenByDescending(p => p.AverageRating)
.ThenByDescending(p => p.CreatedAt)

// ❌ Wrong — missing pin sort or wrong direction
.OrderByDescending(p => p.CreatedAt)
```

Apply this order in:
- `PromptService.GetAllAsync`
- `PromptService.SearchAsync` (pins first, then by similarity score)
- Any new endpoint or partial that returns a list of prompts

---

## 5. Rating — Stored as Total + Count, Not Average

**Rule:** The `Prompt` model stores `RatingTotal` (int) and `RatingCount` (int).
The average is the **computed property** `AverageRating = RatingTotal / RatingCount`.
Never store a pre-computed float average in the database.

```csharp
// ✅ Correct — update total and count on vote
prompt.RatingTotal += stars;
prompt.RatingCount++;

// ❌ Wrong — storing average directly
prompt.AverageRating = newAverage;  // This property doesn't exist as a stored column
```

**Where to check:** `PromptService.RateAsync` and any new rating-related code.

---

## 6. Authentication & Ownership

**Rule:** Server-side ownership must be enforced in `PromptService`, not just hidden in the UI.

```csharp
// ✅ Correct — service throws on unauthorized access
if (prompt.OwnerId != userId)
    throw new UnauthorizedAccessException($"User {userId} does not own prompt {id}.");

// ❌ Wrong — only UI hides the button, no server check
```

**Callers** (page models) must handle `UnauthorizedAccessException` and return `Forbid()`:

```csharp
catch (UnauthorizedAccessException)
{
    return Forbid();
}
```

**Where to check:** `Edit.cshtml.cs`, `Delete.cshtml.cs`, and any new page that modifies a prompt.

---

## 7. Razor Page Handler Naming

**Rule:** Follow ASP.NET Core Razor Pages naming conventions.

| Purpose | Handler name |
|---|---|
| Page load (GET) | `OnGetAsync` |
| Form submit (POST) | `OnPostAsync` |
| AJAX rate | `OnPostRateAsync` |
| AJAX pin toggle | `OnPostTogglePinAsync` |
| Custom AJAX | `OnPost<Verb>Async` |

Handlers that serve AJAX endpoints must return `IActionResult`, not `void` or `PageResult`.

---

## 8. Bootstrap 5 UI Conventions

**Rule:** New UI components must use Bootstrap 5.

- Prompt list: **card layout** (`<div class="card">`)
- Pinned indicator: 📌 badge/icon on the card (`<span class="badge">` or inline emoji)
- Rating display: 5-star row (★☆) with `data-` attributes for JS targeting
- Forms: Bootstrap form controls (`form-control`, `form-label`, `is-invalid`)
- Do **not** introduce custom CSS frameworks or component libraries

**Where to check:** Any new or modified `.cshtml` file under `PromptBank/Pages/`.

---

## 9. AJAX Endpoints — Rate Limiting

**Rule:** All AJAX POST handlers exposed to unauthenticated or authenticated users must use
the `"ajax"` rate limiter attribute:

```csharp
[EnableRateLimiting("ajax")]
public async Task<IActionResult> OnPostRateAsync(...)
```

The `"ajax"` policy allows 30 requests per 60-second window (configured in `Program.cs`).

---

## 10. Embedding Field Rules

**Rule:** Any new `byte[]` field that stores an embedding vector must:

1. Be **nullable** (`byte[]?`) — it may be null for prompts created before the feature
2. Be populated in `PromptService.CreateAsync` and `PromptService.UpdateAsync`
3. Have a corresponding **backfill block** in `Program.cs` for existing rows (see `update-seed-data` skill)

---

## Quick Audit Checklist

Run through this list for every PR or changed file:

- [ ] All new `public` members have `<summary>`, `<param>`, `<returns>` XML docs
- [ ] Page models contain **no** database queries — all delegated to `IPromptService`
- [ ] New `IPromptService` methods are defined on the interface with full XML docs
- [ ] Validation uses DataAnnotations only — no FluentValidation
- [ ] Prompt lists are sorted: pinned → rating desc → createdAt desc
- [ ] Rating logic updates `RatingTotal` + `RatingCount`, not a stored average
- [ ] Edit/Delete handlers enforce ownership server-side and return `Forbid()` on violation
- [ ] New AJAX handlers have `[EnableRateLimiting("ajax")]`
- [ ] New UI uses Bootstrap 5 card/form components
- [ ] New embedding fields are nullable and have a `Program.cs` backfill
