# Spec: Prompt Description Field

**Status:** 📋 Planned  
**Spec file:** `.github/specs/prompt-description.md`

---

## Problem Statement

Each prompt currently only has a **Title** and **Content**. There is no dedicated field to communicate *why* a prompt exists or what scenario it is designed for. This spec adds a **Description** field to every prompt so authors can explain the purpose, use-case, or expected output of the prompt in plain language — separate from the prompt text itself.

---

## Functional Requirements

### FR-1: Description Field on the `Prompt` Entity
- Add a `Description` property to the `Prompt` model.
- `Description` is **required** and has a maximum length of **500 characters**.
- The field is persisted via a new EF Core migration.

### FR-2: Create Form
- The Create Prompt form includes a `Description` textarea between the **Title** and **Prompt Content** fields.
- Validation errors (missing value, exceeds 500 chars) are shown inline.
- Label: *"Description"*, placeholder: *"Briefly describe the purpose of this prompt…"*.

### FR-3: Edit Form
- The Edit Prompt form includes the same `Description` textarea, pre-populated with the current value.
- The `UpdateAsync` service method accepts and saves the updated description.

### FR-4: Prompt Card Display
- The description is displayed in full on each prompt card, between the card header (title/pin badge) and the prompt content block.
- Style: `text-muted small` — visually distinguishable from the prompt content.
- No truncation or "show more" toggle.

### FR-5: Seed Data
- All 5 seeded prompts are updated to include a meaningful `Description` value.

---

## Non-Functional Requirements

### NFR-1: Validation via DataAnnotations
- Use `[Required]` and `[MaxLength(500)]` on the model and input model.
- Do not introduce FluentValidation.

### NFR-2: XML Documentation
- All new or modified public members must have XML doc comments (`<summary>`, `<param>`, `<returns>` as applicable).

---

## Data Model Changes

### Modified: `Prompt`
```csharp
public class Prompt
{
    // ... existing fields ...

    /// <summary>Gets or sets a short description of the prompt's purpose.</summary>
    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
}
```

### Modified: `PromptInputModel` (Create & Edit)
```csharp
/// <summary>Gets or sets the description of the prompt's purpose.</summary>
[Required]
[MaxLength(500)]
[Display(Name = "Description")]
public string Description { get; set; } = string.Empty;
```

---

## Service Layer Changes

| Method | Change |
|---|---|
| `CreateAsync(Prompt prompt)` | No signature change — `Description` is set on the `Prompt` object before calling |
| `UpdateAsync(id, userId, title, description, content, ct)` | Add `description` parameter; update `prompt.Description` |

---

## Page / Handler Changes

| Page | Change |
|---|---|
| `Prompts/Create.cshtml` | Add Description textarea between Title and Prompt Content |
| `Prompts/Create.cshtml.cs` | Add `Description` to `PromptInputModel`; pass to `CreateAsync` |
| `Prompts/Edit.cshtml` | Add Description textarea between Title and Prompt Content |
| `Prompts/Edit.cshtml.cs` | Add `Description` to input model; pass to `UpdateAsync` |
| `Index.cshtml` | Render `p.Description` on each prompt card |

---

## Migration

```bash
dotnet ef migrations add AddPromptDescription --project PromptBank
dotnet ef database update --project PromptBank
```

---

## Acceptance Criteria

| # | Criterion |
|---|---|
| AC-1 | Creating a prompt without a description shows a validation error |
| AC-2 | A description exceeding 500 characters shows a validation error |
| AC-3 | A newly created prompt displays its description on the home page card |
| AC-4 | Editing a prompt allows updating the description; the updated value appears on the card |
| AC-5 | The description appears in full on the card (no truncation) between the title and the prompt content |
| AC-6 | All 5 seeded prompts have a non-empty description |
| AC-7 | Unit tests cover: create with description, update description, validation of required/maxlength |
