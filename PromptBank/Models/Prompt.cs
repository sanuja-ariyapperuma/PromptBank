using System.ComponentModel.DataAnnotations;

namespace PromptBank.Models;

/// <summary>
/// Represents a single Copilot prompt stored in the Prompt Bank.
/// </summary>
public class Prompt
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the short descriptive title of the prompt.</summary>
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets a short description of the prompt's purpose.</summary>
    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the full prompt text.</summary>
    [Required]
    [MaxLength(4000)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Gets or sets the ID of the user who owns this prompt.</summary>
    [Required]
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>Gets or sets the navigation property to the owning user.</summary>
    public ApplicationUser? Owner { get; set; }

    /// <summary>Gets or sets the collection of per-user pins on this prompt.</summary>
    public ICollection<UserPromptPin> Pins { get; set; } = new List<UserPromptPin>();

    /// <summary>Gets or sets the collection of per-user ratings on this prompt.</summary>
    public ICollection<UserPromptRating> Ratings { get; set; } = new List<UserPromptRating>();

    /// <summary>Gets or sets the cumulative sum of all star votes (1–5 each).</summary>
    public int RatingTotal { get; set; }

    /// <summary>Gets or sets the total number of star votes cast.</summary>
    public int RatingCount { get; set; }

    /// <summary>Gets or sets the UTC date and time when the prompt was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the serialized embedding vector for <see cref="Title"/> + <see cref="Description"/>.
    /// Null until the embedding has been computed. Used for semantic search.
    /// </summary>
    public byte[]? TitleDescriptionEmbedding { get; set; }

    /// <summary>
    /// Calculates the average star rating for this prompt.
    /// Returns 0 when no votes have been cast.
    /// </summary>
    public double AverageRating => RatingCount == 0 ? 0 : Math.Round((double)RatingTotal / RatingCount, 1);
}
