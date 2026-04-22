namespace PromptBank.Models;

/// <summary>
/// Represents a per-user pin on a prompt. Composite primary key (UserId, PromptId).
/// </summary>
public class UserPromptPin
{
    /// <summary>Gets or sets the ID of the user who pinned the prompt.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the ID of the pinned prompt.</summary>
    public int PromptId { get; set; }

    /// <summary>Gets or sets the user navigation property.</summary>
    public ApplicationUser? User { get; set; }

    /// <summary>Gets or sets the prompt navigation property.</summary>
    public Prompt? Prompt { get; set; }
}
