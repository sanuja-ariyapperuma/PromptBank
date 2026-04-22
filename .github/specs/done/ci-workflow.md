# Spec: CI Workflow

**Status:** ✅ Done  
**Spec file:** `.github/specs/ci-workflow.md`

---

## Problem Statement

The repository has no continuous integration workflow. Unit tests (`PromptBank.UnitTests`) and
Playwright E2E tests (`PromptBank.Tests`) are only run locally today. Without a CI workflow,
broken code can be merged to `main` without detection.

This spec defines a GitHub Actions CI workflow that builds the solution and runs both test suites
automatically on every push to `main` and on every pull request targeting `main`.

---

## Functional Requirements

### FR-1: Trigger
The workflow runs on:
- `push` to the `main` branch
- `pull_request` targeting the `main` branch

### FR-2: Runner
Use `ubuntu-latest` (GitHub-hosted). No Azure credentials or external services are required —
tests run entirely in-process with an in-memory SQLite database.

### FR-3: .NET Version
Use .NET 10 (`dotnet-version: '10.x'`) via `actions/setup-dotnet@v4`, matching the project's
`<TargetFramework>net10.0</TargetFramework>`.

### FR-4: Build Step
Run a Release build of the full solution after restoring NuGet packages:
```
dotnet restore
dotnet build --no-restore --configuration Release
```
The build must succeed before any tests run.

### FR-5: Unit Tests
Run `PromptBank.UnitTests` using the built output:
```
dotnet test PromptBank.UnitTests --no-build --configuration Release
```

### FR-6: Playwright E2E Tests
Run `PromptBank.Tests` using the built output:
```
dotnet test PromptBank.Tests --no-build --configuration Release
```
`PlaywrightFixture.InitializeAsync()` already calls
`Microsoft.Playwright.Program.Main(["install", "chromium"])` internally, so no separate browser
install step is needed in the workflow.

### FR-7: Headless Chromium in CI
`PlaywrightFixture` must detect the `CI` environment variable (automatically set to `"true"` on
all GitHub-hosted runners) and switch Chromium to headless mode:

```csharp
var isCI = Environment.GetEnvironmentVariable("CI") == "true";
Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
{
    Headless = isCI,
    SlowMo   = isCI ? 0 : 100
});
```

Without this change the Chromium launch will fail on GitHub Actions because the runner has no display.

### FR-8: Permissions
The workflow requires only `contents: read`. No Azure credentials or GitHub secrets are needed.

### FR-9: Workflow File Location
```
.github/workflows/ci.yml
```

### FR-10: Workflow Style
Follow the same header comment style and structure as `.github/workflows/infra.yml`:
- Top comment block explaining triggers and authentication model
- `permissions` block explicitly scoped to minimum required
- Named steps with clear `name:` labels

---

## Non-Functional Requirements

### NFR-1: No Secrets
The CI workflow must not require any GitHub Actions secrets. All tests use SQLite in-memory and
the seeded test users (`alice`, `bob`, `carol`) defined in `PromptBankWebFactory`.

### NFR-2: Fast Feedback
Build and unit tests should give feedback within a few minutes. E2E tests run after unit tests
pass to avoid unnecessary Playwright overhead when there are basic failures.

### NFR-3: Idempotent
Re-running the workflow (e.g., via "Re-run jobs") must produce the same result. No shared state
persists between runs.

---

## File Changes

| File | Change |
|---|---|
| `.github/workflows/ci.yml` | New — CI workflow |
| `PromptBank.Tests/Infrastructure/PlaywrightFixture.cs` | Modify `InitializeAsync` to use headless mode when `CI=true` |

---

## Acceptance Criteria

| # | Criterion |
|---|---|
| AC-1 | Workflow triggers on push to `main` |
| AC-2 | Workflow triggers on pull request targeting `main` |
| AC-3 | A build failure causes the workflow to fail and no tests run |
| AC-4 | Unit test failure causes the workflow to fail |
| AC-5 | E2E test failure causes the workflow to fail |
| AC-6 | All tests pass on a clean run with no local state |
| AC-7 | No GitHub Actions secrets are required |
| AC-8 | Workflow completes successfully on `ubuntu-latest` |
| AC-9 | Chromium launches in headless mode (no display errors in workflow logs) |
