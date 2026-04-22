namespace PromptBank.Services;

/// <summary>
/// Configuration options for the semantic search feature.
/// Bind from the <c>Search</c> section of <c>appsettings.json</c>.
/// </summary>
public class SearchOptions
{
    /// <summary>
    /// Gets or sets the minimum cosine similarity score (0–1) required for a prompt
    /// to appear in search results. Defaults to 0.25.
    /// </summary>
    public float SimilarityThreshold { get; set; } = 0.25f;
}
