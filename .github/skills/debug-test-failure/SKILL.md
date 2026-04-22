---
name: debug-test-failure
description: >
  Use this skill when tests are failing and you need to diagnose why. Covers both
  unit test failures (PromptBank.UnitTests) and E2E Playwright failures (PromptBank.Tests),
  with PromptBank-specific debugging techniques.
---

# Debugging Test Failures in PromptBank

There are two distinct test projects. Always determine which is failing first, then
follow the appropriate section below.

---

## Identify the failing test(s)

```powershell
# Run all tests and capture output
dotnet test --logger "console;verbosity=detailed" 2>&1 | Select-String -Pattern "FAILED|Error|Exception" -Context 2,2
```

Or run each project separately to isolate:

```powershell
dotnet test PromptBank.UnitTests --logger "console;verbosity=detailed"
dotnet test PromptBank.Tests     --logger "console;verbosity=detailed"
```

---

## Section A — Unit Test Failures (`PromptBank.UnitTests`)

### A1. Re-run the specific failing test

```powershell
dotnet test PromptBank.UnitTests --filter "FullyQualifiedName~<FailingTestName>" --logger "console;verbosity=detailed"
```

### A2. Common root causes

**InMemory vs SQLite differences**

Unit tests use `Microsoft.EntityFrameworkCore.InMemory`. The InMemory provider:
- Does **not** enforce `[Required]` / NOT NULL constraints — a test may pass even if a required field is missing
- Does **not** enforce `[MaxLength]` constraints
- Does **not** support `COLLATE` or SQL functions

If a test is trying to verify validation, use model-level validation explicitly:
```csharp
var validationResults = new List<ValidationResult>();
var isValid = Validator.TryValidateObject(prompt, new ValidationContext(prompt), validationResults, true);
```

**Mocking `IEmbeddingService`**

`PromptService` takes `IEmbeddingService`. If not mocked, tests will throw. Always mock it:
```csharp
var mockEmbedder = new Mock<IEmbeddingService>();
mockEmbedder.Setup(e => e.GetEmbeddingBytes(It.IsAny<string>())).Returns(new byte[384]);
mockEmbedder.Setup(e => e.Similarity(It.IsAny<byte[]>(), It.IsAny<byte[]>())).Returns(0.9f);
```

**Sorting order assertion failures**

Prompts sort: **pinned first → avg rating desc → createdAt desc**.
If a test asserts order, check that `RatingTotal` and `RatingCount` are set consistently and
that `UserPromptPin` rows are added to the in-memory DB before calling `GetAllAsync`.

**`AverageRating` returns 0 unexpectedly**

`AverageRating` is a computed property: `RatingCount == 0 ? 0 : Math.Round((double)RatingTotal / RatingCount, 1)`.
Seed both `RatingTotal` and `RatingCount` when testing rating logic.

### A3. Check for stale test state

Each test should create its own `AppDbContext` backed by a **unique** in-memory DB name:
```csharp
var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseInMemoryDatabase(Guid.NewGuid().ToString())
    .Options;
```
If tests share a database name, state leaks between tests. This causes order-dependent failures.

---

## Section B — E2E Playwright Failures (`PromptBank.Tests`)

### B1. Re-run the specific failing test

```powershell
dotnet test PromptBank.Tests --filter "FullyQualifiedName~<FailingTestName>" --logger "console;verbosity=detailed"
```

### B2. Read the Playwright trace

Playwright saves traces to `TestResults/` on failure. To open in the Playwright trace viewer:

```powershell
# Build first to ensure playwright.ps1 is up to date
dotnet build PromptBank.Tests

# Open trace viewer (replace the path with the actual .zip in TestResults/)
pwsh PromptBank.Tests\bin\Debug\net10.0\playwright.ps1 show-trace TestResults\<trace-file>.zip
```

The trace shows: DOM snapshots, network requests, console errors, and a timeline of actions.

### B3. Watch it happen (browser is already non-headless)

`PlaywrightFixture` hardcodes `Headless = false, SlowMo = 100` — the browser always runs visibly.
No env var is needed. To slow down or pause for interactive debugging, edit `PlaywrightFixture.cs`:

```csharp
// Increase SlowMo to see each action clearly
Headless = false,
SlowMo = 500   // ms — increase as needed

// Or pause at a specific point inside a test:
await Page.PauseAsync();  // opens Playwright Inspector
```

Then run the failing test normally:
```powershell
dotnet test PromptBank.Tests --filter "FullyQualifiedName~<FailingTestName>"
```

### B4. Common root causes

**Seeded data not available**

E2E tests must call `Factory.SeedAsync(...)` or `Factory.SeedWithEmbeddingsAsync(...)` in
`InitializeAsync`. If a test fails with "element not found" on the prompt list, check:
- `InitializeAsync` is implemented and `await`ed
- The correct seeded user owns the prompt (use `alice` as default prompt owner)

Available seeded users (created by `PromptBankWebFactory`):
| Username | Password   |
|----------|------------|
| alice    | Alice@1234 |
| bob      | Bob@1234   |
| carol    | Carol@1234 |

**Login not persisting**

Each test gets its own `IBrowserContext` (isolated cookies). Always call the login helper
within the test's context before accessing authenticated pages. Check `E2ETestBase` for the
`LoginAsync` helper and ensure it's being called.

**Element selector is stale or wrong**

Use Playwright's `page.Locator(...)` with data attributes or semantic selectors over CSS class
selectors (Bootstrap classes may change). If a selector fails, open the trace and inspect the DOM.

**Timing / `WaitForSelector` timeout**

AJAX-driven UI (ratings, pin toggle) requires waiting for the DOM update. Use:
```csharp
await page.WaitForSelectorAsync("[data-rating-count]");
// or
await Expect(page.Locator(".alert-success")).ToBeVisibleAsync();
```
Avoid `Task.Delay` — use Playwright's built-in async waiting.

**Two-host in-memory DB conflict**

`PromptBankWebFactory` starts two hosts that share a named SQLite in-memory database via
`cache=shared`. If you see "table already exists" errors, check that `Test:SkipDatabaseInit=true`
is set in the test factory — this prevents double schema creation.

**Playwright browsers not installed**

```powershell
dotnet build PromptBank.Tests
pwsh PromptBank.Tests\bin\Debug\net10.0\playwright.ps1 install chromium
```

### B5. If a test is flaky (passes sometimes, fails sometimes)

1. Run it 5 times in a row to confirm it's flaky: `dotnet test PromptBank.Tests --filter "..." -- xunit.maxParallelThreads=1`
2. Look for: hardcoded waits, timing-sensitive assertions, missing `await` on Playwright actions
3. Replace any `Task.Delay` with Playwright's `WaitForAsync` / `ToBeVisibleAsync` patterns

---

## Section C — Build Failures (neither project compiles)

```powershell
dotnet build 2>&1 | Select-String "error"
```

Common causes:
- A model property change broke a page model or service (check `PromptBank/Pages/` and `PromptBank/Services/`)
- A migration was generated but the corresponding model change has a compile error
- Missing XML doc comment on a new public member (warnings-as-errors is not currently enabled, but keep docs complete anyway)
