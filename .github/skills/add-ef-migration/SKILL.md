---
name: add-ef-migration
description: >
  Use this skill when a model change requires a new EF Core migration. Covers the
  full safe workflow: generate migration → review → build → apply → update Program.cs
  seed/backfill blocks when necessary.
---

# Adding an EF Core Migration in PromptBank

Always follow these steps **in order**. Never skip the build or review steps.

---

## Step 1 — Understand the model change

Before generating a migration, confirm:

- Which model(s) in `PromptBank/Models/` changed (added/removed/renamed properties)?
- Was `AppDbContext` changed (new `DbSet`, `OnModelCreating` rules)?
- Does the new field need a **default value** for existing rows?
- Is the new field a `byte[]` (embedding)? → requires a Program.cs backfill (see Step 5).
- Is the new field a non-nullable string? → EF will default it to `""` on migration; verify that's acceptable.

---

## Step 2 — Generate the migration

Always pass `--project PromptBank`. Migration files go to `PromptBank/Migrations/`.

```powershell
dotnet ef migrations add <MigrationName> --project PromptBank
```

**Naming conventions:**
| Change | Example name |
|---|---|
| Add a property | `AddPromptCategory` |
| Add a table | `AddTagsTable` |
| Remove a property | `RemovePromptLegacyField` |
| Rename / structural | `RefactorPromptSchema` |

---

## Step 3 — Review the generated migration file

Open `PromptBank/Migrations/<timestamp>_<MigrationName>.cs` and verify:

- **`Up()`** performs exactly the changes you intended — no extra columns, no dropped columns you didn't intend.
- **`Down()`** correctly reverses the `Up()` operation.
- If a new non-nullable column was added to an existing table, EF will include a `defaultValue` — confirm it's sensible (e.g., `""` for strings, `0` for ints, `null` for nullable types).
- **Do NOT manually edit migration files** unless correcting an obvious EF generation error.

> ⚠️ If the migration looks wrong (e.g., drops a column unexpectedly), delete it with:
> ```powershell
> dotnet ef migrations remove --project PromptBank
> ```
> Fix the model and regenerate.

---

## Step 4 — Build to confirm no compile errors

```powershell
dotnet build
```

The build must succeed before proceeding. Fix any compilation errors — do not apply a migration against a broken build.

---

## Step 5 — Update `Program.cs` seed and backfill logic

Open `PromptBank/Program.cs` and check both seed blocks:

### A. New field on `Prompt` — update the initial seed block

If you added a field to `Prompt`, add it to the five seeded prompts in the `if (!dbForSeed.Prompts.Any())` block:

```csharp
new() { Title = "Explain code", /* ... */ NewField = <appropriate seed value>, ... },
```

### B. New field needs backfill for existing rows — add a backfill block

Add a backfill block **inside the `else` branch** (after the description backfill, before the embedding backfill block that runs unconditionally below the if/else), following this pattern:

```csharp
// Backfill <FieldName> for prompts that pre-date this field
var needsBackfill = dbForSeed.Prompts.Where(p => p.<FieldName> == <default/null>).ToList();
foreach (var p in needsBackfill)
    p.<FieldName> = <computed or default value>;
if (needsBackfill.Count > 0)
    await dbForSeed.SaveChangesAsync();
```

### C. New `byte[]` embedding field — use the embedding service pattern

```csharp
var needsEmbedding = dbForSeed.Prompts.Where(p => p.<EmbeddingField> == null).ToList();
foreach (var p in needsEmbedding)
    p.<EmbeddingField> = embeddingService.GetEmbeddingBytes(<source text from p>);
if (needsEmbedding.Count > 0)
    await dbForSeed.SaveChangesAsync();
```

> The `IEmbeddingService` is already resolved earlier in the seed block as `embeddingService`.

---

## Step 6 — Apply the migration (dev/local only)

```powershell
dotnet ef database update --project PromptBank
```

> In production, `MigrateAsync()` is called automatically at app startup (`Program.cs`).
> You do **not** need to run this manually for production deploys.

---

## Step 7 — Verify the app runs

```powershell
dotnet run --project PromptBank
```

Navigate to the app and confirm:
- No startup exceptions
- Existing prompts display correctly (backfill worked)
- New field is visible/functional where expected

---

## Common Pitfalls

| Problem | Fix |
|---|---|
| `No migrations configuration type was found` | Ensure you used `--project PromptBank`, not the solution root |
| Migration adds unexpected columns | You may have unsaved model changes from a previous edit — check `AppDbContextModelSnapshot.cs` |
| `dotnet ef` not found | Install with: `dotnet tool install --global dotnet-ef` |
| Build fails after migration | Check the `.cs` migration file for syntax EF may have generated incorrectly |
| Existing rows get null for new required field | Add a backfill block in `Program.cs` (Step 5B) and verify the migration has a `defaultValue` |
