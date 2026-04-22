---
name: run-tests
description: >
  Use this skill when asked to run tests, check test results, verify a fix with tests,
  or when you need to know if the build or tests are passing. Covers unit tests
  (PromptBank.UnitTests) and E2E Playwright tests (PromptBank.Tests).
---

# Running Tests in PromptBank

This repo has two test projects. Always run unit tests first — they are faster and
have no external dependencies.

---

## 1. Unit Tests (`PromptBank.UnitTests`)

Uses xUnit + Moq + EF Core InMemory. No browser or server required.

```powershell
# Run all unit tests
dotnet test PromptBank.UnitTests

# Run a single test by name (partial match is fine)
dotnet test PromptBank.UnitTests --filter "FullyQualifiedName~<TestName>"

# Run with coverage
dotnet test PromptBank.UnitTests --collect:"XPlat Code Coverage"
```

Test files are in:
- `PromptBank.UnitTests/Services/PromptServiceTests.cs` — sorting, rating, pin, search logic
- `PromptBank.UnitTests/Pages/` — page model handler tests (Index, Create, Edit, Delete)
- `PromptBank.UnitTests/Models/PromptModelTests.cs` — model validation and computed properties

---

## 2. E2E Playwright Tests (`PromptBank.Tests`)

Uses xUnit + Microsoft.Playwright + WebApplicationFactory. Spins up a real Kestrel server
on a random loopback port with a named SQLite in-memory database — no production DB touched.

### First-time setup (install Chromium binaries)
```powershell
dotnet build PromptBank.Tests
pwsh PromptBank.Tests\bin\Debug\net10.0\playwright.ps1 install chromium
```

> Only needed once per machine. The `PlaywrightFixture` also calls `playwright install chromium`
> automatically at runtime, but running it manually avoids a slow first run.

### Run all E2E tests
```powershell
dotnet test PromptBank.Tests
```

### Run a single E2E test by name
```powershell
dotnet test PromptBank.Tests --filter "FullyQualifiedName~<TestName>"
```

E2E test files are in `PromptBank.Tests/Tests/`:
| File | What it covers |
|---|---|
| `AuthenticationTests.cs` | Login, logout, registration flows |
| `CreatePromptTests.cs` | Create form validation and submission |
| `EditPromptTests.cs` | Edit ownership enforcement (403) |
| `DeletePromptTests.cs` | Delete ownership enforcement (403) |
| `PromptListTests.cs` | Pinned prompts sort to top |
| `StarRatingTests.cs` | AJAX star rating updates in place |
| `PinToggleTests.cs` | Pin/unpin AJAX toggle |
| `CopyTests.cs` | Clipboard copy button |
| `SearchTests.cs` | Semantic search results |
| `NavigationTests.cs` | Nav bar links and redirects |
| `DarkThemeTests.cs` | Dark/light theme toggle |
| `ShowMoreTests.cs` | Prompt content expand/collapse |
| `PromptDescriptionTests.cs` | Description field display |

---

## 3. Run Everything (unit + E2E)

```powershell
dotnet test
```

This runs all projects in the solution (`PromptBank.UnitTests` + `PromptBank.Tests`).

---

## Key Infrastructure Notes

- **E2E tests use a named SQLite in-memory DB** (not the production `promptbank.db` file).
  `PromptBankWebFactory` opens a sentinel `SqliteConnection` to keep the DB alive across the test run.
- **Two hosts per E2E test class**: a TestServer (for `HttpClient`) and a real Kestrel host
  (for Playwright). Both share the same named in-memory DB via `cache=shared`.
- **Seeded users** available in E2E tests: `alice / Alice@1234`, `bob / Bob@1234`, `carol / Carol@1234`.
- **Browser**: Chromium launched by `PlaywrightFixture` with `Headless = false, SlowMo = 100ms` — runs
  visibly by default. All tests in the `"Playwright"` collection share one browser process; each test
  gets its own `IBrowserContext`.
- **`Test:SkipDatabaseInit=true`** is set in `PromptBankWebFactory` to prevent double
  schema-creation when both hosts start against the same in-memory DB.
