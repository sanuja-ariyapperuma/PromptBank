---
name: implement-feature
description: >
  Use this when asked to implement a feature, spec, or user story end-to-end.
  Applies the full PromptBank implementation workflow: feature code → unit tests →
  E2E tests → infrastructure → spec completion. Do NOT skip or reorder steps.
---

# PromptBank Feature Implementation Workflow

Follow these steps **in order**. Do not proceed to the next step until the current
step is verified (tests passing, build succeeds).

---

## Before you start — Plan mode

If you are in **plan mode**, create a spec file before writing the plan:
- Location: `.github/specs/<feature-name>.md` (kebab-case filename)
- The spec must capture the feature requirements and all acceptance criteria.
- Do NOT start implementation until the spec exists and the user has confirmed the plan.

---

## Spec lifecycle

```
.github/specs/          ← active specs (planned or in progress)
.github/specs/done/     ← completed specs (all ACs verified, tests passing)
```

**Never move a spec to `done/` prematurely.** The presence of a spec in `specs/` signals
work is still outstanding. Moving it is the final act of Step 7 — not before.

---

## Step 1 — Read the spec

- Find the spec file in `.github/specs/` (active) or ask the user which feature to implement.
- Read every acceptance criterion (AC-1, AC-2, ...) carefully.
- List the ACs explicitly before writing any code — they are the definition of done.
- Note which ACs require backend logic, which require UI changes, and which require
  infrastructure changes. This determines which later steps apply.

---

## Step 2 — Implement the feature code

Apply all conventions from `.github/copilot-instructions.md`. Key rules:

**Architecture**
- Add new business logic to `PromptBank/Services/PromptService.cs` and its interface `IPromptService.cs`.
- Keep page models thin — page models call the service; they do not contain query or business logic.
- New Razor Pages go in `PromptBank/Pages/` (CRUD pages under `Pages/Prompts/`).
- New models go in `PromptBank/Models/`.

**Code style**
- Every public class, method, and property **must** have XML doc comments (`<summary>`,
  `<param>`, `<returns>`).
- Use `[Required]`, `[MaxLength]` DataAnnotations for validation — do not introduce FluentValidation.
- Razor Page handler naming: `OnGet`, `OnPost`, `OnPostXxxAsync` (e.g. `OnPostRateAsync`).
- Use Bootstrap 5 card layout for any new UI components.

**Database**
- If the feature adds or changes a model property, create an EF Core migration:
  ```powershell
  dotnet ef migrations add <MigrationName> --project PromptBank
  ```
- Do NOT manually edit migration files.
- Verify the app still builds after migration:
  ```powershell
  dotnet build
  ```

---

## Step 3 — Write and run unit tests

Write one unit test per acceptance criterion that can be verified without a browser.
Typical targets: service logic, sorting, validation, computed properties, error cases.

**Setup rules**
- Test project: `PromptBank.UnitTests/`
- Use `Microsoft.EntityFrameworkCore.InMemory` to back `AppDbContext` — never the SQLite file.
- Use `Moq` to mock `IEmbeddingService` or other injected dependencies where needed.
- Place service tests in `PromptBank.UnitTests/Services/`, page model tests in `PromptBank.UnitTests/Pages/`.

**Run tests**
```powershell
# Run only the new tests first
dotnet test PromptBank.UnitTests --filter "FullyQualifiedName~<NewTestClassName>"

# Then run the full unit test suite to check for regressions
dotnet test PromptBank.UnitTests
```

✅ All unit tests must be green before moving to Step 4.

**Common unit test targets in this codebase**
- `PromptService` sorting logic (pinned first → avg rating desc → date desc)
- Rating calculation (`RatingTotal / RatingCount`, guard for zero `RatingCount`)
- Validation — `Title`, `Content` required; `Title` max 200 chars
- `TogglePinAsync` — pin then unpin returns correct boolean each time
- `RateAsync` — duplicate vote returns `null`; out-of-range stars throws
- `SearchAsync` — results filtered by similarity threshold, ordered correctly

---

## Step 4 — Write and run E2E tests

Write one Playwright test per acceptance criterion that requires browser interaction
(UI rendering, AJAX behaviour, navigation, clipboard, authentication flows).

**Setup rules**
- Test project: `PromptBank.Tests/`
- All test classes must inherit `E2ETestBase` and use `[Collection("Playwright")]`.
- Use `PromptBankWebFactory` for the server — do NOT start `dotnet run` manually.
- Seed test data via `SeedAsync(...)` or `SeedWithEmbeddingsAsync(...)` — protected helpers on `E2ETestBase` that delegate to the factory. Call them after `await base.InitializeAsync()` in your test class's `InitializeAsync` override, or directly from within a test method.
- Seeded test users available in every test (created by `PromptBankWebFactory`):
  | Username | Password   |
  |----------|------------|
  | alice    | Alice@1234 |
  | bob      | Bob@1234   |
  | carol    | Carol@1234 |
- Use `alice` as the default prompt owner; use `bob` to test ownership enforcement (403 scenarios).
- Place new test files in `PromptBank.Tests/Tests/`.

**First-time Playwright browser install (if not already done)**
```powershell
dotnet build PromptBank.Tests
pwsh PromptBank.Tests\bin\Debug\net10.0\playwright.ps1 install chromium
```

**Run tests**
```powershell
# Run only the new E2E tests first
dotnet test PromptBank.Tests --filter "FullyQualifiedName~<NewTestClassName>"

# Then run the full E2E suite to check for regressions
dotnet test PromptBank.Tests
```

✅ All E2E tests must be green before moving to Step 5.

**Common E2E scenarios in this codebase**
- Prompt list loads with pinned prompts at the top
- One-click copy writes prompt content to clipboard (no server round-trip)
- Star rating updates average and count in place — no full page reload
- Create form rejects a missing required field and shows an inline validation error
- Edit/Delete buttons are hidden for non-owners; server returns 403 if forced
- Semantic search returns relevant results when a query is entered
- Dark/light theme toggle persists across navigation

---

## Step 5 — Infrastructure (only if the feature requires it)

Apply this step when the feature introduces any of the following:

| Trigger | Action |
|---|---|
| New app setting or secret | Add to `infra/modules/appService.bicep` `appSettings` block |
| New Key Vault secret | Add secret resource to `infra/modules/keyVault.bicep` |
| New connection string | Update `infra/parameters/dev.bicepparam` and `prod.bicepparam` |
| Schema change (new migration) | Document in deployment notes — `dotnet ef database update` runs on deploy via app startup |
| New environment variable | Add to both `appsettings.json` (with placeholder) and the Bicep app settings |

**Validate Bicep (if changed)**
```powershell
az bicep build --file infra/main.bicep
```

If `infra/` does not yet exist (spec `bicep-infrastructure.md` not yet implemented),
note the required infrastructure change as a comment in the code (e.g. `// TODO: add to Bicep when infra is provisioned`).

---

## Step 6 — Final verification

Run the complete test suite one last time to confirm nothing is broken end-to-end:

```powershell
dotnet test
```

All projects (`PromptBank.UnitTests` + `PromptBank.Tests`) must pass.

---

## Step 7 — Move the spec to done

This step is **mandatory**. A feature is not complete until the spec is moved.

```powershell
Move-Item .github\specs\<spec-name>.md .github\specs\done\<spec-name>.md
```

Then confirm to the user: *"All acceptance criteria implemented and verified.
Spec moved to `.github/specs/done/`."*
