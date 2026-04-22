---
name: update-seed-data
description: >
  Use this skill when you need to change or extend the seed data in Program.cs —
  adding new seeded prompts, updating existing ones, or adding a backfill block
  for a new model field on existing rows.
---

# Updating Seed Data in PromptBank

All seed and backfill logic lives in `PromptBank/Program.cs`, inside the
`if (!app.Configuration.GetValue<bool>("Test:SkipDatabaseInit"))` guard.

There are three distinct operations covered below. Identify which applies to your change.

---

## Structure of the Seed Block

```
Program.cs seed block (runs on every startup for relational DBs)
│
├── MigrateAsync()                          ← applies pending EF migrations
├── Seed users (alice, bob, carol)          ← idempotent: skips if user exists
├── if (!dbForSeed.Prompts.Any())
│   └── Initial seed block                 ← runs ONLY when the table is empty
│       ├── Seed 5 prompts
│       └── Seed 2 pins
├── else
│   └── Backfill blocks                    ← runs when rows exist but fields are missing
│       ├── Description backfill (dictionary lookup)
│       └── ... (add new backfill blocks here)
│
└── Embedding backfill                     ← always runs; skips rows that already have embeddings
```

---

## Operation 1 — Add a new seeded prompt

Add to the `prompts` list inside the `if (!dbForSeed.Prompts.Any())` block.
Follow the existing pattern exactly:

```csharp
new() {
    Title       = "Your prompt title",
    Description = "One-sentence description of what this prompt is for.",
    Content     = "The prompt text with {{placeholder}} markers.",
    OwnerId     = alice!.Id,          // use alice, bob, or carol
    RatingTotal = 20,                 // realistic seed values
    RatingCount = 5,
    CreatedAt   = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)
},
```

**Rules:**
- `Description` is required and max 500 chars
- `Content` max 4000 chars
- `CreatedAt` must be UTC (`DateTimeKind.Utc`)
- Distribute ownership between alice, bob, and carol
- Choose `RatingTotal` / `RatingCount` so that `AverageRating` (total÷count) gives a realistic value (e.g. 4.0 = 20/5)
- After adding the prompt, optionally add a pin:
  ```csharp
  dbForSeed.UserPromptPins.Add(new UserPromptPin { UserId = alice!.Id, PromptId = prompts[<index>].Id });
  ```

---

## Operation 2 — Add a backfill block for a new model field

When you add a new property to `Prompt` (via an EF migration), existing rows will have the
default/null value for that field. Add a backfill block so existing data is upgraded on next startup.

Place the new backfill inside the `else` branch (after the description backfill), following this pattern:

### Pattern A: Simple value backfill (string/int/bool)

```csharp
// Backfill <FieldName> for prompts that pre-date this field
var needsFieldBackfill = dbForSeed.Prompts
    .Where(p => string.IsNullOrEmpty(p.<FieldName>))  // or == null / == 0
    .ToList();
foreach (var p in needsFieldBackfill)
    p.<FieldName> = <default or computed value>;
if (needsFieldBackfill.Any(p => /* field was actually updated */))
    await dbForSeed.SaveChangesAsync();
```

### Pattern B: Dictionary-based backfill (known values per title)

Use this when the backfill value depends on the prompt title and you know the mapping:

```csharp
var <fieldName>Map = new Dictionary<string, <Type>>(StringComparer.OrdinalIgnoreCase)
{
    ["Explain code"]          = <value>,
    ["Write unit tests"]      = <value>,
    ["Code review checklist"] = <value>,
    ["Generate SQL query"]    = <value>,
    ["Summarise PR"]          = <value>,
};
var needs<FieldName>Backfill = dbForSeed.Prompts
    .Where(p => p.<FieldName> == <default>)
    .ToList();
foreach (var p in needs<FieldName>Backfill)
{
    if (<fieldName>Map.TryGetValue(p.Title, out var val))
        p.<FieldName> = val;
}
if (needs<FieldName>Backfill.Any(p => p.<FieldName> != <default>))
    await dbForSeed.SaveChangesAsync();
```

### Pattern C: Embedding (`byte[]`) backfill

Always use this pattern for new embedding fields. The `embeddingService` variable is already
available in the seed block:

```csharp
var needs<FieldName>Embedding = dbForSeed.Prompts
    .Where(p => p.<EmbeddingField> == null)
    .ToList();
foreach (var p in needs<FieldName>Embedding)
    p.<EmbeddingField> = embeddingService.GetEmbeddingBytes($"{p.Title} {p.<SourceTextField>}");
if (needs<FieldName>Embedding.Count > 0)
    await dbForSeed.SaveChangesAsync();
```

> The embedding backfill for `TitleDescriptionEmbedding` already exists at the bottom of the seed
> block. If you add a **second** embedding field, add a separate block for it — do not combine them.

---

## Operation 3 — Update an existing seeded prompt

Edit the matching entry in the `if (!dbForSeed.Prompts.Any())` seed list.

> ⚠️ The initial seed block only runs when the `Prompts` table is **empty**. Changing seed data
> does **not** update existing rows in a running database. If you need to update existing rows,
> add a backfill block (Operation 2) that detects the old value and replaces it.

---

## Verification

After making changes to the seed block:

1. **Delete the local database** to force a clean seed:
   ```powershell
   Remove-Item PromptBank\promptbank.db* -ErrorAction SilentlyContinue
   ```

2. **Run the app** and confirm the seeded data appears correctly:
   ```powershell
   dotnet run --project PromptBank
   ```

3. **Run unit tests** — they use InMemory and are not affected by seed changes.

4. **Run E2E tests** — they use a separate named in-memory SQLite database seeded by
   `PromptBankWebFactory`, not by `Program.cs`. If your change affects E2E test setup,
   update `Factory.SeedAsync(...)` or `Factory.SeedWithEmbeddingsAsync(...)` in the test factory too.

---

## Seeded Users Reference

These three users are always created by the seed block (and by `PromptBankWebFactory` for tests):

| Username | Password   | Email |
|----------|------------|-------|
| alice    | Alice@1234 | alice@promptbank.local |
| bob      | Bob@1234   | bob@promptbank.local |
| carol    | Carol@1234 | carol@promptbank.local |

Convention: use **alice** as the default prompt owner, **bob** to test ownership enforcement (403 scenarios).
