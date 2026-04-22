# Prompt Bank 📚

> **Your team's shared GitHub Copilot prompt library** — a collaborative, internally-hosted web application where engineers store, discover, rate, and reuse the Copilot prompts that actually work.

---

## Table of Contents

- [Business Case](#business-case)
- [Feature Overview](#feature-overview)
- [Architecture](#architecture)
- [Data Model](#data-model)
- [Technology Stack](#technology-stack)
- [Getting Started](#getting-started)
- [Testing Strategy](#testing-strategy)
- [CI/CD Pipeline](#cicd-pipeline)
- [Deployment Architecture](#deployment-architecture)
- [GitHub Copilot CLI — How This Project Was Built](#github-copilot-cli--how-this-project-was-built)
- [Project Structure](#project-structure)
- [Configuration Reference](#configuration-reference)
- [Contributing](#contributing)

---

## Business Case

Adopting GitHub Copilot at scale surfaces a repeatable friction point: **prompt quality is inconsistent across the team**. Senior engineers craft prompts that unlock Copilot's full potential — generating production-ready unit tests, reviewing code for security flaws, summarising PRs in seconds. Junior engineers repeat trial-and-error every day. That institutional knowledge disappears when people switch teams or leave.

**Prompt Bank solves this.** It is a lightweight, self-hosted web application that functions as a living prompt library for your engineering organisation:

| Problem | Solution |
|---|---|
| Prompt quality varies wildly across the team | Surface the best prompts via community star ratings |
| Useful prompts live in Slack threads and die there | Persistent, searchable, attributed storage |
| Reusing a prompt requires finding it again | One-click copy to clipboard, no friction |
| Finding the right prompt requires remembering exact keywords | Semantic (meaning-based) search — describe what you want in plain English |
| Everyone pins different prompts | Per-user pinning that surfaces your favourites to the top |
| Anyone could accidentally delete another engineer's prompt | Full ownership model — only the author can edit or delete |

The result is a self-reinforcing flywheel: good prompts get discovered, rated, and reused; bad prompts sink; the team's collective Copilot effectiveness compounds over time.

---

## Feature Overview

### 🔍 Semantic Search
Type a natural-language query like *"write tests for async methods"* and instantly find relevant prompts — even when the title uses completely different words. Powered by a locally-bundled ONNX sentence-transformer model (`all-MiniLM-L6-v2` via `SmartComponents.LocalEmbeddings`) — **no external API, no internet call at runtime**.

### ⭐ Community Star Ratings
Rate any prompt 1–5 stars. The average score and vote count update live via AJAX — no page reload. Prompts are sorted by rating so the highest-quality content naturally rises to the top.

### 📌 Per-User Pinning
Pin the prompts you reach for every day. Your pins appear at the top of the list — only visible to you. Pinning is a personal preference, not a global flag. Toggle on/off via AJAX with instant feedback.

### 🔐 Ownership & Authentication
ASP.NET Core Identity with local accounts. Cookie-based sliding 7-day sessions. Prompts are owned by the user who created them — only the owner sees Edit and Delete buttons, and the server enforces this with a 403 if the check is bypassed.

### 📋 One-Click Copy
Every prompt card has a **Copy** button that writes the full prompt content to the clipboard via the Web Clipboard API — zero server round-trip.

### 🌙 Dark / Light Theme
Theme preference is persisted to `localStorage` and restored before first paint — no flash of wrong theme.

### 📄 Prompt Descriptions
Every prompt carries a short description explaining its purpose — searchable and displayed on the card. Long prompt content is collapsible with a **Show more / Show less** toggle.

---


## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    Browser (Bootstrap 5)                 │
│   Razor Pages (.cshtml)  ·  Vanilla JS (clipboard/AJAX) │
└───────────────────────┬─────────────────────────────────┘
                        │ HTTPS
┌───────────────────────▼─────────────────────────────────┐
│           ASP.NET Core 10 — Razor Pages                  │
│                                                          │
│   Pages/              Services/          Data/           │
│   ├── Index           ├── PromptService  └── AppDbContext│
│   ├── Prompts/Create  ├── EmbeddingService               │
│   ├── Prompts/Edit    └── IPromptService                 │
│   ├── Prompts/Delete                                     │
│   └── Account/        Rate limiting (fixed window)       │
│       ├── Login        30 req/min on AJAX endpoints      │
│       ├── Register                                       │
│       └── Logout                                         │
│                                                          │
│   ASP.NET Core Identity (cookie auth, 7-day sliding)     │
└───────────────────────┬─────────────────────────────────┘
                        │ EF Core 10
┌───────────────────────▼─────────────────────────────────┐
│                    SQLite                                │
│   dev:  promptbank.db (local file)                       │
│   prod: /home/data/promptbank.db (Azure Files mount)     │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│   SmartComponents.LocalEmbeddings (ONNX, in-process)     │
│   all-MiniLM-L6-v2 sentence-transformer model            │
│   Embeddings stored as byte[] in SQLite                  │
│   Cosine similarity at query time — no external API      │
└─────────────────────────────────────────────────────────┘
```

### Key Architectural Decisions

| Decision | Choice | Rationale |
|---|---|---|
| UI framework | Razor Pages | Simple CRUD — no SPA complexity needed |
| ORM | EF Core code-first | Migrations, LINQ, trivial provider swap |
| Default database | SQLite | Zero-config local dev; persistent on Azure `/home` |
| Semantic search | Local ONNX model | No API keys, no latency, no cost, no data egress |
| Frontend JS | Vanilla JS only | Clipboard API + AJAX rating/pin — no framework needed |
| CSS | Bootstrap 5 + Bootstrap Icons | Responsive cards, dark/light theme, zero custom overhead |
| Business logic | `PromptService` / `IPromptService` | Fully decoupled from page models; unit-testable with in-memory DB |
| Authentication | ASP.NET Core Identity | Battle-tested; local accounts only; no OAuth complexity |
| Rating storage | `RatingTotal` + `RatingCount` (ints) | Append-only aggregate; average computed on read |
| Ownership enforcement | Server-side 403 | Client-side hiding is UX, not security |

---

## Data Model

```
ApplicationUser (IdentityUser)
    │
    ├── Prompt  (1:many via OwnerId)
    │     ├── Id            int PK
    │     ├── Title         string  [Required, MaxLength(200)]
    │     ├── Description   string  [Required, MaxLength(500)]
    │     ├── Content       string  [Required, MaxLength(4000)]
    │     ├── OwnerId       string  FK → ApplicationUser.Id
    │     ├── RatingTotal   int     (sum of all votes)
    │     ├── RatingCount   int     (number of votes)
    │     ├── CreatedAt     DateTime UTC
    │     ├── TitleDescriptionEmbedding  byte[]?  (ONNX vector, nullable)
    │     ├── AverageRating  double  (computed: RatingTotal / RatingCount)
    │     ├── Pins          ICollection<UserPromptPin>
    │     └── Ratings       ICollection<UserPromptRating>
    │
    ├── UserPromptPin  (composite PK: UserId + PromptId)
    │     ├── UserId   FK → ApplicationUser.Id
    │     └── PromptId FK → Prompt.Id
    │
    └── UserPromptRating  (composite PK: UserId + PromptId)
          ├── UserId   FK → ApplicationUser.Id
          ├── PromptId FK → Prompt.Id
          └── Stars    int (1–5; immutable once cast)
```

**Embedding storage:** Each prompt stores a pre-computed float32 embedding of `Title + Description` as a raw `byte[]` column. At search time, the query is embedded in-process using the same model, and cosine similarity is computed in memory. No vector database required.

---

## Technology Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 |
| Web framework | ASP.NET Core 10, Razor Pages |
| ORM | Entity Framework Core 10 |
| Database | SQLite (swappable to any EF-supported provider) |
| Identity | ASP.NET Core Identity (`IdentityDbContext<ApplicationUser>`) |
| Semantic search | `SmartComponents.LocalEmbeddings` 0.1.0-preview (`all-MiniLM-L6-v2` ONNX) |
| Frontend | Bootstrap 5.3, Bootstrap Icons 1.11, Vanilla JS |
| Unit tests | xUnit, Moq, EF Core InMemory |
| E2E tests | Playwright via `Microsoft.Playwright` + xUnit |
| Infrastructure | Azure Bicep (modular) |
| CI/CD | GitHub Actions (3 separate workflows) |
| Cloud platform | Azure App Service (Linux, B1/B2) |

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Entity Framework CLI tools](https://learn.microsoft.com/en-us/ef/core/cli/dotnet): `dotnet tool install --global dotnet-ef`

### Clone and run

```bash
git clone https://github.com/sanuja-ariyapperuma/promptbank.git
cd promptbank

dotnet build
dotnet run --project PromptBank
```

The app starts at `https://localhost:5001`. The database is created automatically on first run.

**Seeded demo accounts:**

| Username | Password    |
|----------|-------------|
| `alice`  | `Alice@1234`|
| `bob`    | `Bob@1234`  |
| `carol`  | `Carol@1234`|

### Database migrations

```bash
# Add a new migration after a model change
dotnet ef migrations add <MigrationName> --project PromptBank

# Apply pending migrations manually
dotnet ef database update --project PromptBank
```

Migrations run automatically on app startup via `MigrateAsync()`.

### Switching the database provider

The app defaults to SQLite. To switch to SQL Server (or any EF Core provider):

1. Replace `Microsoft.EntityFrameworkCore.Sqlite` with the target provider NuGet package
2. Update `builder.Services.AddDbContext` in `Program.cs` (`UseSqlServer(...)`, etc.)
3. Update the connection string in `appsettings.json`
4. Re-run migrations — no model changes required

---

## Testing Strategy

The project has two test layers: fast in-process unit tests and full end-to-end browser tests. Both run in CI on every push and pull request.

### Unit Tests — `PromptBank.UnitTests`

- **Framework:** xUnit + Moq
- **Database:** EF Core `InMemory` provider (unit tests) or in-memory SQLite via `SqliteConnection("DataSource=:memory:")` (service tests that need real SQL translation semantics)
- **Coverage targets:** `PromptService` business logic, Razor Page model handlers, computed properties, validation rules

```bash
dotnet test PromptBank.UnitTests
# with coverage
dotnet test PromptBank.UnitTests --collect:"XPlat Code Coverage"
# single test
dotnet test PromptBank.UnitTests --filter "FullyQualifiedName~GetAllAsync_SortsCorrectly"
```

**What is tested:**
- Sorting: pinned-first → avg rating desc → date desc
- Rating: `RateAsync` returns null on duplicate vote; throws on out-of-range stars
- Pin toggle: returns correct boolean on each call
- Semantic search: results filtered by similarity threshold; pinned results ordered first
- Validation: `[Required]`, `[MaxLength]` DataAnnotations on all model fields
- Ownership: `UpdateAsync` / `DeleteAsync` throw `UnauthorizedAccessException` for non-owners

### E2E Tests — `PromptBank.Tests`

- **Framework:** xUnit + `Microsoft.Playwright` (Chromium)
- **Server:** `WebApplicationFactory<Program>` spins up a real Kestrel server on a random loopback port — no `dotnet run` needed
- **Database:** Named in-memory SQLite (`cache=shared`) shared across the test and server hosts
- **Architecture note:** Two hosts run per test class (TestServer for `HttpClient`, Kestrel for Playwright). `Test:SkipDatabaseInit=true` prevents double-initialisation.

```bash
# First-time Playwright browser install
dotnet build PromptBank.Tests
pwsh PromptBank.Tests\bin\Debug\net10.0\playwright.ps1 install chromium

# Run all E2E tests
dotnet test PromptBank.Tests

# Run a specific test file
dotnet test PromptBank.Tests --filter "FullyQualifiedName~SearchTests"
```

| Test File | Scenarios Covered |
|---|---|
| `AuthenticationTests.cs` | Login, logout, registration, redirect-to-login enforcement |
| `CreatePromptTests.cs` | Form validation, successful submission, ownership assignment |
| `EditPromptTests.cs` | Edit form, ownership enforcement (403 on forced POST) |
| `DeletePromptTests.cs` | Delete confirmation, ownership enforcement (403) |
| `PromptListTests.cs` | Pinned prompts sort to top; empty state message |
| `StarRatingTests.cs` | AJAX star rating updates average and count in place |
| `PinToggleTests.cs` | AJAX pin/unpin with badge and button state update |
| `CopyTests.cs` | Clipboard copy button writes correct content |
| `SearchTests.cs` | Semantic search returns relevant results; empty-query shows all |
| `NavigationTests.cs` | Nav bar links, authentication state changes, redirects |
| `DarkThemeTests.cs` | Dark/light theme toggle persists across navigation |
| `ShowMoreTests.cs` | Long prompt content collapse/expand |
| `PromptDescriptionTests.cs` | Description field display on list and form pages |

### Run everything

```bash
dotnet test
```

---

## CI/CD Pipeline

The project uses three purpose-built GitHub Actions workflows, designed for clean separation of concerns between code validation, application delivery, and infrastructure lifecycle.

```
┌─────────────────────────────────────────────────────────────────┐
│                         GitHub Events                           │
│                                                                 │
│  push/PR → main          push/PR → main      manual / infra/**  │
│       │                       │                      │          │
│       ▼                       │                      ▼          │
│  ┌─────────┐                  │            ┌──────────────────┐ │
│  │   CI    │                  │            │   Infrastructure │ │
│  │ Workflow│                  │            │   Provisioning   │ │
│  └────┬────┘                  │            └────────┬─────────┘ │
│       │ on: completed         │                     │           │
│       ▼                       │                     ▼           │
│  ┌─────────────────────────┐  │         Azure Bicep deployment  │
│  │        CD Workflow      │  │         (dev auto / prod manual)│
│  │  ┌──────────┐           │  │                                 │
│  │  │ deploy   │ ← PR CI   │  │                                 │
│  │  │  dev     │           │  │                                 │
│  │  └──────────┘           │  │                                 │
│  │  ┌──────────┐           │  │                                 │
│  │  │ deploy   │ ← push CI │  │                                 │
│  │  │  prod    │ (approval │  │                                 │
│  │  └──────────┘  required)│  │                                 │
│  └─────────────────────────┘  │                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Workflow 1: `ci.yml` — Continuous Integration

Triggers on every **push** to `main` and every **pull request** targeting `main`.

```
Checkout → Setup .NET 10 → dotnet restore → dotnet build (Release)
  → dotnet test PromptBank.UnitTests
  → dotnet test PromptBank.Tests (Playwright E2E)
  → dotnet publish → Upload artifact (7-day retention)
```

- No GitHub secrets required — all tests run in-process with an in-memory SQLite database
- Produces a publish artifact `promptbank-app` consumed by the CD workflow

### Workflow 2: `cd.yml` — Continuous Deployment

Triggers automatically when the CI workflow **completes** (via `workflow_run` event).

| CI trigger | CD outcome |
|---|---|
| Pull request CI passes | Deploys to **dev** environment automatically |
| Push to `main` CI passes | Deploys to **prod** environment (requires reviewer approval via GitHub Environment protection rule) |

**Authentication:** OIDC / Workload Identity Federation — **no stored Azure credentials**. The workflow exchanges a short-lived OIDC token for an Azure access token at runtime. Required GitHub secrets: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`.

```yaml
- name: Azure Login (OIDC)
  uses: azure/login@v2
  with:
    client-id: ${{ secrets.AZURE_CLIENT_ID }}
    tenant-id: ${{ secrets.AZURE_TENANT_ID }}
    subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

- name: Deploy to Azure App Service
  run: |
    az webapp deploy \
      --name app-promptbank-${ENVIRONMENT} \
      --resource-group rg-promptbank-${ENVIRONMENT} \
      --type zip \
      --src-path app.zip
```

### Workflow 3: `infra.yml` — Infrastructure Provisioning

Triggers when files under `infra/**` change on `main`, or manually via `workflow_dispatch` with an environment choice (`dev` / `prod`).

```
Checkout → Azure Login (OIDC) → az bicep build (validate)
  → az deployment group create (idempotent Bicep deployment)
```

- The deploying service principal's object ID is resolved at runtime and passed to Bicep for role assignment

---

## Deployment Architecture

```
Azure
│
├── Resource Group: rg-promptbank-dev
│   ├── App Service Plan: asp-promptbank-dev  (B1 SKU, Linux)
│   └── App Service: app-promptbank-dev
│         ├── HTTPS enforced, TLS 1.2 minimum
│         ├── .NET 10 runtime
│         ├── ASPNETCORE_ENVIRONMENT = Development
│         ├── ConnectionStrings__DefaultConnection
│         │     = Data Source=/home/data/promptbank.db
│         └── WEBSITE_RUN_FROM_PACKAGE = 1
│
└── Resource Group: rg-promptbank-prod
    ├── App Service Plan: asp-promptbank-prod  (B2 SKU, Linux)
    └── App Service: app-promptbank-prod
          ├── HTTPS enforced, TLS 1.2 minimum
          ├── .NET 10 runtime
          ├── ASPNETCORE_ENVIRONMENT = Production
          ├── ConnectionStrings__DefaultConnection
          │     = Data Source=/home/data/promptbank.db
          └── WEBSITE_RUN_FROM_PACKAGE = 1
```

**SQLite on Azure App Service:** The database file is stored at `/home/data/promptbank.db`. The `/home` mount is backed by Azure Files and **persists across app restarts and redeployments**. `Program.cs` creates the directory at startup if it doesn't exist.

**Zero-downtime deployment:** The app is deployed as a zip package with `WEBSITE_RUN_FROM_PACKAGE=1`. The package is atomically swapped — the old process continues serving until the new one is ready.

**EF migrations on startup:** `MigrateAsync()` runs automatically on app startup, so schema updates are applied without a separate deployment step or manual intervention.

### Infrastructure as Code

All resources are defined in modular Bicep:

```
infra/
├── main.bicep                  # Entry point — wires modules together
├── main.json                   # Compiled ARM template (auto-generated)
├── modules/
│   ├── appServicePlan.bicep    # Windows App Service Plan (B1 dev / B2 prod)
│   └── appService.bicep        # Web App with SQLite connection string
└── parameters/
    ├── dev.bicepparam           # environment=dev, location=uksouth
    └── prod.bicepparam          # environment=prod, location=uksouth
```

The `environment` parameter drives all naming (`rg-promptbank-{env}`, `app-promptbank-{env}`) and SKU selection (`B1` for dev, `B2` for prod), so the same Bicep templates serve both environments with no duplication.

---

## GitHub Copilot CLI — How This Project Was Built

Prompt Bank was built end-to-end using the **GitHub Copilot CLI** (`gh copilot`) as the primary development tool. This section documents the full Copilot-assisted engineering workflow so the approach can be replicated on other projects.

### Copilot Instructions (`.github/copilot-instructions.md`)

The repository contains a rich Copilot instructions file that persists project context across every session. It documents:

- Tech stack and build commands
- Data model with all fields and relationships
- Sorting rules, validation conventions, authentication decisions
- XML doc comment requirements for all public members
- Bootstrap usage conventions and Razor Page handler naming patterns
- How to switch database providers

This file acts as the project's **ambient context** — Copilot reads it automatically and applies the conventions without being reminded each time.

### Custom Agents (`.github/agents/`)

Four specialist agents were created to handle distinct domains of engineering work:

| Agent | Role | When Invoked |
|---|---|---|
| `dotnet-security-architect` | Reviews .NET architecture for security gaps and over-engineering | "Is this architecture secure?", "Am I over-engineering this?" |
| `frontend-ux-designer` | Designs UX-first frontend components with accessibility and responsiveness | "Make this look better", "Design an accessible form" |
| `test-coverage-engineer` | Writes high-coverage unit tests following AAA pattern | "Write unit tests for", "Improve test coverage" |
| `ui-automation-qa` | Creates comprehensive Playwright E2E test suites | "Write UI automation tests for this feature" |

Each agent has a detailed instruction file defining its persona, methodology, decision-making framework, quality checklist, and output format. This means the same specialist "mindset" is available consistently across every session and every team member.

**Example agent invocation:**
> "Write unit tests for the new `SearchAsync` method" → `test-coverage-engineer` agent analyses the code, identifies all execution paths (threshold filtering, pin-first ordering, empty results), and writes xUnit tests with in-memory SQLite.

### Skills (`.github/skills/`)

Skills encode repeatable multi-step workflows as executable playbooks:

| Skill | Purpose |
|---|---|
| `implement-feature` | Full feature workflow: spec → code → unit tests → E2E tests → infra → move spec to `done/` |
| `run-tests` | Knows both test projects, filter syntax, first-time Playwright setup, and all infrastructure notes |
| `check-conventions` | Audits code against all conventions before committing |
| `add-ef-migration` | Safe migration workflow: generate → review → build → apply → update seed/backfill |
| `debug-test-failure` | Diagnoses both unit test and Playwright failures |
| `update-seed-data` | Extends or modifies seed data in `Program.cs` |

The `implement-feature` skill is the crown jewel — it enforces a strict, ordered workflow:

```
Step 1: Read the spec (list all ACs)
Step 2: Implement feature code (conventions from copilot-instructions.md)
Step 3: Unit tests (must be green before proceeding)
Step 4: E2E Playwright tests (must be green before proceeding)
Step 5: Infrastructure changes (if needed)
Step 6: Final full test suite
Step 7: Move spec to done/
```

This workflow was used to implement every feature in the project — authentication, semantic search, dark mode, CI/CD, Bicep infrastructure, and more.

### Spec-Driven Development (`.github/specs/`)

Every feature started as a spec file in `.github/specs/`. Specs capture:
- Problem statement
- Acceptance criteria (numbered AC-1, AC-2, ...)
- Data model changes
- Service layer changes
- UI changes
- Migration strategy

Completed specs live in `.github/specs/done/` as a permanent record of what was built and why. The `specs/done/` folder effectively doubles as a decision log for the project.

**Delivered features (all spec-driven):**

| Spec | Feature |
|---|---|
| `initialrequirement.md` | Core CRUD, star ratings, pin/unpin, copy to clipboard, seed data |
| `user-authentication.md` | ASP.NET Core Identity, ownership, per-user pins/ratings |
| `semantic-search.md` | ONNX local embeddings, semantic search, similarity threshold |
| `dark-theme.md` | Dark/light theme toggle, localStorage persistence |
| `prompt-description.md` | Description field, semantic search over title+description |
| `single-column-layout.md` | Full-width prompt cards, show more/less content toggle |
| `ci-workflow.md` | GitHub Actions CI with unit + E2E tests |
| `cd-workflow.md` | GitHub Actions CD with OIDC and environment approval |
| `bicep-infrastructure.md` | Modular Azure Bicep, App Service, SQLite on `/home` |
| `e2e-test-coverage.md` | Full E2E test suite across all 13 scenario files |

### Playwright MCP Server (`.github/copilot/mcp.json`)

Playwright is configured as an MCP (Model Context Protocol) server, giving Copilot the ability to **drive a real browser** during development. This means Copilot can:

- Navigate to a page and observe the live UI
- Interact with forms, buttons, and AJAX controls
- Take screenshots during test development
- Verify that E2E tests pass against the running app

```json
{
  "mcpServers": {
    "playwright": {
      "command": "npx",
      "args": ["@playwright/mcp@latest"]
    }
  }
}
```

---

## Project Structure

```
prompt bank/
├── .github/
│   ├── agents/
│   │   ├── dotnet-security-architect.agent.md
│   │   ├── frontend-ux-designer.agent.md
│   │   ├── test-coverage-engineer.agent.md
│   │   └── ui-automation-qa.agent.md
│   ├── copilot/
│   │   └── mcp.json                          # Playwright MCP server
│   ├── copilot-instructions.md               # Persistent project context for Copilot
│   ├── skills/
│   │   ├── implement-feature/SKILL.md
│   │   ├── run-tests/SKILL.md
│   │   ├── check-conventions/SKILL.md
│   │   ├── add-ef-migration/SKILL.md
│   │   ├── debug-test-failure/SKILL.md
│   │   └── update-seed-data/SKILL.md
│   ├── specs/
│   │   └── done/                             # Completed feature specs
│   └── workflows/
│       ├── ci.yml                            # Build + test + publish
│       ├── cd.yml                            # Deploy to dev / prod (OIDC)
│       └── infra.yml                         # Bicep provisioning
│
├── PromptBank/                               # Main application
│   ├── Data/
│   │   └── AppDbContext.cs                   # IdentityDbContext<ApplicationUser>
│   ├── Migrations/                           # EF Core migrations
│   ├── Models/
│   │   ├── ApplicationUser.cs
│   │   ├── Prompt.cs
│   │   ├── UserPromptPin.cs
│   │   └── UserPromptRating.cs
│   ├── Pages/
│   │   ├── Account/                          # Login, Register, Logout
│   │   ├── Prompts/                          # Create, Edit, Delete
│   │   ├── Index.cshtml(.cs)                 # Listing + AJAX handlers
│   │   └── Shared/                           # Layout, partials
│   ├── Services/
│   │   ├── IPromptService.cs
│   │   ├── PromptService.cs                  # All business logic
│   │   ├── IEmbeddingService.cs
│   │   ├── EmbeddingService.cs               # ONNX wrapper
│   │   └── SearchOptions.cs
│   ├── wwwroot/js/site.js                    # Clipboard + AJAX rating/pin/theme
│   ├── Program.cs                            # DI, middleware, seed data
│   └── appsettings.json
│
├── PromptBank.UnitTests/                     # xUnit unit tests
│   ├── Models/PromptModelTests.cs
│   ├── Pages/                                # Page model handler tests
│   └── Services/PromptServiceTests.cs
│
├── PromptBank.Tests/                         # xUnit + Playwright E2E tests
│   ├── Infrastructure/
│   │   ├── E2ETestBase.cs
│   │   ├── PlaywrightFixture.cs
│   │   └── PromptBankWebFactory.cs
│   └── Tests/                                # 13 E2E test files
│
└── infra/                                    # Azure Bicep IaC
    ├── main.bicep
    ├── modules/
    │   ├── appService.bicep
    │   └── appServicePlan.bicep
    └── parameters/
        ├── dev.bicepparam
        └── prod.bicepparam
```

---

## Configuration Reference

### `appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=promptbank.db"
  },
  "Search": {
    "SimilarityThreshold": 0.25
  }
}
```

**`Search:SimilarityThreshold`** — Cosine similarity cutoff (0–1) for semantic search results. Lower values return more results with less precision; higher values return fewer, more closely matching results. Default: `0.25`.

### Environment Variables (App Service)

| Variable | Description |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Development` or `Production` |
| `ConnectionStrings__DefaultConnection` | SQLite path (e.g. `Data Source=/home/data/promptbank.db`) |
| `WEBSITE_RUN_FROM_PACKAGE` | `1` — enables atomic zip deployment |

### GitHub Secrets (for CD and infra workflows)

| Secret | Required by | Description |
|---|---|---|
| `AZURE_CLIENT_ID` | `cd.yml`, `infra.yml` | OIDC app registration client ID |
| `AZURE_TENANT_ID` | `cd.yml`, `infra.yml` | Azure AD tenant ID |
| `AZURE_SUBSCRIPTION_ID` | `cd.yml`, `infra.yml` | Target Azure subscription |

---

## Contributing

1. Fork the repository and create a feature branch
2. Read `.github/copilot-instructions.md` — it defines all conventions
3. Follow the spec-driven workflow: create a spec in `.github/specs/` before writing code
4. Use the `implement-feature` skill for end-to-end implementation
5. Ensure all tests pass: `dotnet test`
6. Submit a pull request — CI will run automatically

**Code conventions checklist:**
- [ ] Every public class, method, and property has XML doc comments (`<summary>`, `<param>`, `<returns>`)
- [ ] Business logic lives in `PromptService`, not in page models
- [ ] New model fields use DataAnnotations (`[Required]`, `[MaxLength]`) — no FluentValidation
- [ ] New Razor Page handlers follow `OnGet` / `OnPost` / `OnPostXxxAsync` naming
- [ ] Any model change includes an EF Core migration
- [ ] Feature is covered by both unit tests and E2E Playwright tests

---

*Prompt Bank — your internal GitHub Copilot prompt library 📚*
