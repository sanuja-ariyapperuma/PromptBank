namespace PromptBank.Models;

/// <summary>
/// Represents a per-user star rating on a prompt. Composite primary key (UserId, PromptId).
/// </summary>
public class UserPromptRating
{
    /// <summary>Gets or sets the ID of the user who rated the prompt.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the ID of the rated prompt.</summary>
    public int PromptId { get; set; }

    /// <summary>Gets or sets the star rating value (1–5).</summary>
    public int Stars { get; set; }

    /// <summary>Gets or sets the user navigation property.</summary>
    public ApplicationUser? User { get; set; }

    /// <summary>Gets or sets the prompt navigation property.</summary>
    public Prompt? Prompt { get; set; }
}
