using PromptBank.Models;

namespace PromptBank.Services;

/// <summary>Carries the updated rating aggregate returned after a successful vote.</summary>
public sealed record RatingResult(double Average, int Count);

/// <summary>Request model for the rate AJAX handler.</summary>
public sealed record RateRequest(int Id, [System.ComponentModel.DataAnnotations.Range(1, 5)] int Stars);

/// <summary>Request model for the toggle-pin AJAX handler.</summary>
public sealed record TogglePinRequest(int Id);

/// <summary>
/// Defines all business operations for prompt management.
/// </summary>
public interface IPromptService
{
    /// <summary>
    /// Returns all prompts sorted: pinned-for-user first (if userId provided), then by average rating
    /// descending, then by <see cref="Prompt.CreatedAt"/> descending.
    /// </summary>
    /// <param name="userId">The current user's ID for per-user pin ordering, or null for anonymous.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of <see cref="Prompt"/> entities in sort order.</returns>
    Task<IReadOnlyList<Prompt>> GetAllAsync(string? userId = null, CancellationToken ct = default);

    /// <summary>
    /// Returns a single prompt by its primary key.
    /// </summary>
    /// <param name="id">The primary key of the prompt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <see cref="Prompt"/>, or <c>null</c> if not found.</returns>
    Task<Prompt?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Persists a new prompt. <see cref="Prompt.CreatedAt"/> is set by this method.
    /// </summary>
    /// <param name="prompt">The prompt to create.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The saved <see cref="Prompt"/> with its generated <see cref="Prompt.Id"/>.</returns>
    Task<Prompt> CreateAsync(Prompt prompt, CancellationToken ct = default);

    /// <summary>
    /// Updates the title, description, and content of an existing prompt.
    /// </summary>
    /// <param name="id">The primary key of the prompt to update.</param>
    /// <param name="userId">The ID of the user requesting the update.</param>
    /// <param name="title">New title.</param>
    /// <param name="description">New description of the prompt's purpose.</param>
    /// <param name="content">New content.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when <paramref name="id"/> does not exist.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when <paramref name="userId"/> does not own the prompt.</exception>
    Task UpdateAsync(int id, string userId, string title, string description, string content, CancellationToken ct = default);

    /// <summary>
    /// Permanently deletes a prompt.
    /// </summary>
    /// <param name="id">The primary key of the prompt to delete.</param>
    /// <param name="userId">The ID of the user requesting the deletion.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when <paramref name="id"/> does not exist.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when <paramref name="userId"/> does not own the prompt.</exception>
    Task DeleteAsync(int id, string userId, CancellationToken ct = default);

    /// <summary>
    /// Records a star rating vote and returns the updated aggregate.
    /// Returns null if the user has already voted on this prompt.
    /// </summary>
    /// <param name="promptId">The primary key of the prompt to rate.</param>
    /// <param name="userId">The ID of the user casting the vote.</param>
    /// <param name="stars">A value between 1 and 5 inclusive.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="RatingResult"/> with the new average and vote count, or null if already voted.</returns>
    Task<RatingResult?> RateAsync(int promptId, string userId, int stars, CancellationToken ct = default);

    /// <summary>
    /// Toggles the pinned state of a prompt for a specific user.
    /// </summary>
    /// <param name="promptId">The primary key of the prompt to pin or unpin.</param>
    /// <param name="userId">The ID of the user toggling the pin.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if the prompt is now pinned for the user; <c>false</c> if it is now unpinned.</returns>
    Task<bool> TogglePinAsync(int promptId, string userId, CancellationToken ct = default);

    /// <summary>
    /// Returns prompts whose <see cref="Prompt.Title"/> + <see cref="Prompt.Description"/> are
    /// semantically similar to <paramref name="query"/>, sorted with pinned prompts first (when
    /// <paramref name="userId"/> is provided), then by descending similarity score.
    /// </summary>
    /// <param name="query">The natural-language search query.</param>
    /// <param name="userId">The current user's ID for per-user pin ordering, or null for anonymous.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A read-only list of <see cref="Prompt"/> entities whose semantic similarity exceeds the
    /// configured threshold, in order of relevance.
    /// </returns>
    Task<IReadOnlyList<Prompt>> SearchAsync(string query, string? userId = null, CancellationToken ct = default);
}
