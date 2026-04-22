# Semantic Search

## Overview
Add a semantic (meaning-based) search feature to the Prompt Bank index page. Users can type a natural-language query; the system matches prompts whose **Title + Description** are semantically similar, not just keyword-matching.

## Technology
- **SmartComponents.LocalEmbeddings** (v0.1.0-preview10148) — bundles the `all-MiniLM-L6-v2` ONNX model locally. No external API or internet connection required at runtime.

## Fields Searched
- `Prompt.Title` + `Prompt.Description` (concatenated into one embedding per prompt)

## Acceptance Criteria

1. A search box appears at the top of `Index.cshtml` (all users, including anonymous).
2. Submitting the form issues a GET request to `/?q=<query>`.
3. Results are filtered to prompts whose semantic similarity score with the query exceeds a configurable threshold (default **0.25**).
4. Within search results, **pinned prompts appear first** (for authenticated users), then sorted by descending similarity score.
5. If no query is submitted, the full list is shown in the normal sort order (pinned → rating → date).
6. If no results match, a friendly "No results" message is shown.
7. Each `Prompt` stores a pre-computed `TitleDescriptionEmbedding` (`byte[]`), populated on create/update.
8. A startup backfill routine computes embeddings for any existing prompts that have a null embedding.
9. `IPromptService.SearchAsync(string query, string? userId, CancellationToken ct)` is the single entry point for semantic search (injectable, unit-testable).
10. Unit tests cover: matching query returns relevant prompts, unrelated query returns empty, pinned prompts appear first in results.
