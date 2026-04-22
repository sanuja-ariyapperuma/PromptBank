# Copilot Instructions

## Tech Stack

- **Framework:** ASP.NET Core 10, Razor Pages
- **ORM:** Entity Framework Core (code-first)
- **Database:** SQLite (default); designed to swap to any EF-supported SQL database (e.g., SQL Server) by changing the provider package and connection string — no model changes required
- **Frontend:** Razor `.cshtml` + Bootstrap; minimal JavaScript (vanilla JS for clipboard copy and rating)
- **Unit Testing:** xUnit (`PromptBank.UnitTests/`)
- **E2E Testing:** Playwright via xUnit + `Microsoft.Playwright` (`PromptBank.Tests/`)

---

## Build & Run

```bash
dotnet build
dotnet run --project PromptBank
```

### Database migrations

```bash
dotnet ef migrations add <MigrationName> --project PromptBank
dotnet ef database update --project PromptBank
```

### Switching the database provider

The app uses SQLite by default. To switch to SQL Server (or any other EF Core provider):
1. Replace `Microsoft.EntityFrameworkCore.Sqlite` NuGet package with the target provider (e.g., `Microsoft.EntityFrameworkCore.SqlServer`)
2. Update `builder.Services.AddDbContext` in `Program.cs` to use the new provider (`UseSqlServer(...)`, etc.)
3. Update the connection string in `appsettings.json`
4. Re-run migrations — no changes to models or page models required

---

## Unit Testing with xUnit

Unit tests live in `PromptBank.UnitTests/` (xUnit + `Moq` for mocking). Test `PromptService` and any logic that doesn't require a running server.

Use `Microsoft.EntityFrameworkCore.InMemory` to back `AppDbContext` in unit tests — do **not** use the SQLite file.

```bash
# Run all unit tests
dotnet test PromptBank.UnitTests

# Run a single test by name
dotnet test PromptBank.UnitTests --filter "FullyQualifiedName~<TestName>"
```

---

Playwright is configured as an MCP server in `.github/copilot/mcp.json`. Test files live in `PromptBank.Tests/` (xUnit + `Microsoft.Playwright`).

```bash
# Install Playwright browsers (first time)
pwsh PromptBank.Tests/bin/Debug/net10.0/playwright.ps1 install

# Run all E2E tests
dotnet test PromptBank.Tests

# Run a single test by name
dotnet test PromptBank.Tests --filter "FullyQualifiedName~<TestName>"
```

---

## Project Structure

```
PromptBank/
  Pages/
    Index.cshtml(.cs)       # Prompt listing (pinned first, then by rating/date)
    Prompts/
      Create.cshtml(.cs)    # Add new prompt (owner name mandatory)
      Edit.cshtml(.cs)
      Delete.cshtml(.cs)
  Models/
    Prompt.cs               # Core entity (see Data Model below)
  Data/
    AppDbContext.cs
  Services/
    PromptService.cs        # Business logic; unit-testable, injected into page models
  wwwroot/
    js/site.js              # Clipboard copy + AJAX rating calls
PromptBank.UnitTests/       # xUnit unit tests (services, model logic)
PromptBank.Tests/           # xUnit + Playwright E2E tests

---

## Data Model

```csharp
public class Prompt
{
    public int Id { get; set; }
    public string Title { get; set; }              // required, max 200 chars
    public string Content { get; set; }            // the prompt text; required, max 4000 chars
    public string OwnerId { get; set; }            // FK → ApplicationUser.Id
    public ApplicationUser Owner { get; set; }     // navigation property
    public int RatingTotal { get; set; }           // sum of all star votes
    public int RatingCount { get; set; }           // number of votes; avg = RatingTotal / RatingCount
    public DateTime CreatedAt { get; set; }
    public ICollection<UserPromptPin> Pins { get; set; }
    public ICollection<UserPromptRating> Ratings { get; set; }
}

public class UserPromptPin
{
    public string UserId { get; set; }   // FK → ApplicationUser.Id
    public int PromptId { get; set; }    // FK → Prompt.Id
    // Composite PK: (UserId, PromptId)
}

public class UserPromptRating
{
    public string UserId { get; set; }   // FK → ApplicationUser.Id
    public int PromptId { get; set; }    // FK → Prompt.Id
    public int Stars { get; set; }       // 1–5; one vote per user per prompt
    // Composite PK: (UserId, PromptId)
}
```

---

## Key Conventions

### Sorting order
Prompts are always sorted: **pinned first**, then by **descending rating**, then by **descending CreatedAt**. Apply this order in `Index.cshtml.cs` and any API/partial endpoints.

### Copy-to-clipboard
Implemented client-side with the Clipboard API (`navigator.clipboard.writeText`). The copy button lives on each prompt card. No server round-trip.

### Rating
Each prompt has a **1–5 star rating**. A user selects a star and the score is submitted via AJAX `POST` to `OnPostRateAsync`. The handler stores the new rating and returns the updated average (float) and total vote count as JSON; the DOM updates in place without a full page reload. The `Prompt` model stores `RatingTotal` (int) and `RatingCount` (int); average is computed on read.

### Authentication
The app uses **ASP.NET Core Identity** with local accounts (username + password). Cookie-based auth with a sliding 7-day session. Key points:
- `ApplicationUser` extends `IdentityUser`; `AppDbContext` extends `IdentityDbContext<ApplicationUser>`
- Prompts are owned by users via `Prompt.OwnerId` (FK → `ApplicationUser.Id`)
- Only the prompt owner sees Edit/Delete buttons; server enforces ownership (returns 403 otherwise)
- Unauthenticated users can browse; Create/Edit/Delete/Pin/Rate require login
- Per-user pinning via `UserPromptPin` join table; per-user one-vote rating via `UserPromptRating`
- Do **not** add OAuth/social login or role-based authorization

### XML documentation comments
Every public method, property, and class **must** have XML doc comments. Minimum required tags are `<summary>` on all members, plus `<param>` and `<returns>` on methods with parameters/return values.

```csharp
/// <summary>
/// Returns all prompts sorted by pin status, average rating, then creation date descending.
/// </summary>
/// <returns>An ordered list of <see cref="Prompt"/> entities.</returns>
public async Task<List<Prompt>> GetAllSortedAsync() { ... }
```

### Validation
`OwnerName` and `Content` are `[Required]`. `Title` is `[Required]` and `[MaxLength(200)]`. Use DataAnnotations; do not introduce FluentValidation.

### Razor Pages handler naming
Follow the standard `OnGet`, `OnPost`, `OnPostRateAsync`, `OnPostTogglePinAsync` pattern. Keep page models thin — delegate all query and business logic to `PromptService` so it can be unit tested in isolation with an in-memory DB.

### Bootstrap usage
Use Bootstrap 5 card layout for prompt display. Pin indicator: a small 📌 badge or icon on the card. Rating shows a 5-star row (★☆) with the average score and vote count; clicking a star submits the vote via AJAX.
