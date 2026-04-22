# Spec: Prompt Bank — Initial Requirements

**Status:** ✅ Done  
**Implemented:** April 2026  
**Framework:** ASP.NET Core 10, Razor Pages, EF Core, SQLite

---

## Problem Statement

The organisation needed a shared, internal web application where team members could store, discover, and reuse useful GitHub Copilot prompts. No authentication was required — the goal was low friction, high visibility, and collaborative ownership.

---

## Functional Requirements

### FR-1: View All Prompts
- The home page (`/`) lists all prompts stored in the system.
- **Pinned** prompts always appear at the top of the list.
- Within pinned and unpinned groups, prompts are ordered by descending average rating, then by descending creation date.
- Each card shows: title, full prompt content, owner name, creation date, average star rating, and vote count.
- An empty state message is shown when no prompts exist yet.

### FR-2: Add a New Prompt
- Any user can navigate to `/Prompts/Create` to add a prompt.
- Required fields: **Title** (max 200 chars), **Content** (max 4000 chars), **Owner Name** (max 100 chars).
- Optional field: **Pin to top** checkbox.
- Submitting the form with any required field missing shows an inline validation error.
- On success, the user is redirected to the home page.
- `CreatedAt` is set to UTC now on creation — not editable by the user.

### FR-3: Edit a Prompt
- Each prompt card has an **Edit** button linking to `/Prompts/Edit?id={id}`.
- All fields (Title, Content, OwnerName, IsPinned) are editable.
- Same validation rules as Create apply.
- On save, the user is redirected to the home page.

### FR-4: Delete a Prompt
- Each prompt card has a **Delete** button linking to `/Prompts/Delete?id={id}`.
- A confirmation page is shown before the prompt is permanently removed.
- On confirmation, the prompt is deleted and the user is redirected to the home page.

### FR-5: One-Click Copy to Clipboard
- Each prompt card has a **Copy** button.
- Clicking it copies the full prompt `Content` to the clipboard using `navigator.clipboard.writeText` (no server round-trip).
- The button icon/text updates briefly to confirm the copy succeeded.

### FR-6: Star Rating (1–5 Stars)
- Each prompt card displays a 5-star row showing the current average rating.
- A user selects a star (1–5) and the vote is submitted via an AJAX `POST` to `?handler=Rate`.
- The server increments `RatingTotal` and `RatingCount` and returns `{ average, count }` as JSON.
- The DOM updates in place — no full page reload.
- Average is computed as `RatingTotal / RatingCount`; displays `–` when no votes exist.

### FR-7: Pin / Unpin a Prompt
- Each prompt card has a **Pin** / **Unpin** button.
- Clicking it sends an AJAX `POST` to `?handler=TogglePin`.
- The server toggles `IsPinned` and returns `{ isPinned }` as JSON.
- The card's pin badge and button label update in place — no full page reload.

### FR-8: Seed Data
- The database is seeded with 5 sample prompts on first run so the app is immediately usable:
  - "Explain code" (pinned, Dev Team)
  - "Write unit tests" (pinned, QA Team)
  - "Code review checklist" (Architecture Guild)
  - "Generate SQL query" (Data Team)
  - "Summarise PR" (Platform Team)

---

## Non-Functional Requirements

### NFR-1: No Authentication
There is no login system. Owner name is a plain text field entered freely by the user. No `[Authorize]` or ASP.NET Identity scaffolding.

### NFR-2: Database Provider
- **SQLite** is used in all environments (development and production).
- In production (Azure App Service), the database file is stored at `/home/data/promptbank.db` on the persistent `/home` filesystem (backed by Azure Files).
- The provider can be swapped to SQL Server or any EF-supported provider by changing the NuGet package, provider call in `Program.cs`, and connection string — no model or migration changes required.

### NFR-3: Rate Limiting on AJAX Endpoints
- Rating and pin-toggle endpoints are protected by a fixed-window rate limiter (30 requests/minute per client) to prevent vote inflation.

### NFR-4: Auto-Migration on Startup
- On startup, `MigrateAsync()` is called for relational providers.
- `EnsureCreatedAsync()` is called when using an in-memory provider (tests).

### NFR-5: XML Documentation
- All public classes, methods, and properties carry XML doc comments (`<summary>`, `<param>`, `<returns>`).

---

## Data Model

```csharp
public class Prompt
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; }        // Short descriptive title

    [Required, MaxLength(4000)]
    public string Content { get; set; }      // The full prompt text

    [Required, MaxLength(100)]
    public string OwnerName { get; set; }    // Person who submitted the prompt

    public bool IsPinned { get; set; }       // Pinned prompts sort first

    public int RatingTotal { get; set; }     // Sum of all star votes
    public int RatingCount { get; set; }     // Number of votes cast

    public DateTime CreatedAt { get; set; }  // UTC timestamp; set on create

    // Computed, not stored
    public double AverageRating => RatingCount == 0 ? 0 : Math.Round((double)RatingTotal / RatingCount, 1);
}
```

---

## Architecture Decisions

| Decision | Choice | Rationale |
|---|---|---|
| UI framework | Razor Pages | Simple CRUD, no SPA complexity needed |
| ORM | EF Core (code-first) | Migrations, LINQ, easy provider swap |
| Default DB | SQLite | Zero-config local development |
| Frontend JS | Vanilla JS | Minimal dependency; clipboard + AJAX only |
| CSS | Bootstrap 5 + Bootstrap Icons | Consistent, responsive cards with no custom CSS overhead |
| Business logic | `PromptService` / `IPromptService` | Decoupled from page models; unit-testable with in-memory DB |
| Rating storage | `RatingTotal` + `RatingCount` (ints) | Append-only; average computed on read; no per-user vote tracking needed |

---

## Sorting Rule (canonical)

```
ORDER BY IsPinned DESC, (RatingTotal / RatingCount) DESC, CreatedAt DESC
```

Applied in `PromptService.GetAllAsync()` and enforced in unit tests.

---

## Project Structure

```
PromptBank/
  Pages/
    Index.cshtml(.cs)           # Listing + AJAX rating/pin handlers
    Prompts/
      Create.cshtml(.cs)
      Edit.cshtml(.cs)
      Delete.cshtml(.cs)
  Models/
    Prompt.cs
  Data/
    AppDbContext.cs             # EF context + seed data
  Services/
    IPromptService.cs
    PromptService.cs            # All business logic
  wwwroot/js/site.js            # Clipboard copy + AJAX rating/pin
PromptBank.UnitTests/           # xUnit; InMemory EF; Moq
  Services/PromptServiceTests.cs
  Pages/IndexModelTests.cs
  Pages/CreateModelTests.cs
  Pages/EditModelTests.cs
  Pages/DeleteModelTests.cs
  Models/PromptModelTests.cs
PromptBank.Tests/               # xUnit + Playwright E2E
  Infrastructure/
    PromptBankWebFactory.cs
    PlaywrightFixture.cs
    E2ETestBase.cs
  Tests/
    PromptListTests.cs
    CreatePromptTests.cs
    EditPromptTests.cs
    DeletePromptTests.cs
    CopyTests.cs
    StarRatingTests.cs
    PinToggleTests.cs
    NavigationTests.cs
    ShowMoreTests.cs
```

---

## Acceptance Criteria (verified by tests)

| # | Criterion | Covered by |
|---|---|---|
| AC-1 | Prompt list loads; pinned prompts appear first | `PromptListTests`, `PromptServiceTests` |
| AC-2 | Prompts within same pin group ordered by avg rating desc then date desc | `PromptServiceTests.GetAllAsync_SortsCorrectly` |
| AC-3 | Create form rejects missing `OwnerName` with validation error | `CreateModelTests`, `CreatePromptTests` (E2E) |
| AC-4 | Copy button writes prompt content to clipboard (no server call) | `CopyTests` (E2E) |
| AC-5 | Star rating updates average and count in place (no page reload) | `StarRatingTests` (E2E), `PromptServiceTests.RateAsync` |
| AC-6 | Pin toggle updates card badge and button label in place | `PinToggleTests` (E2E), `PromptServiceTests.TogglePinAsync` |
| AC-7 | `AverageRating` returns 0 when `RatingCount` is 0 | `PromptModelTests` |
| AC-8 | Delete confirmation page is shown before removal | `DeletePromptTests` (E2E) |
| AC-9 | Edit updates all fields and redirects to home | `EditPromptTests` (E2E), `EditModelTests` |
