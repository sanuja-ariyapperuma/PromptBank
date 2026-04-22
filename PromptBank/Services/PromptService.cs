using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PromptBank.Data;
using PromptBank.Models;

namespace PromptBank.Services;

/// <summary>
/// Provides all business logic for managing prompts, including sorting, CRUD, rating, pin toggling, and semantic search.
/// </summary>
public class PromptService : IPromptService
{
    private readonly AppDbContext _db;
    private readonly IEmbeddingService _embeddingService;
    private readonly float _similarityThreshold;

    /// <summary>
    /// Initialises a new instance of <see cref="PromptService"/>.
    /// </summary>
    /// <param name="db">The injected database context.</param>
    /// <param name="embeddingService">The injected embedding service for semantic search.</param>
    /// <param name="searchOptions">The injected search configuration options.</param>
    public PromptService(AppDbContext db, IEmbeddingService embeddingService, IOptions<SearchOptions> searchOptions)
    {
        _db = db;
        _embeddingService = embeddingService;
        _similarityThreshold = searchOptions.Value.SimilarityThreshold;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Prompt>> GetAllAsync(string? userId = null, CancellationToken ct = default)
    {
        var prompts = await _db.Prompts
            .Include(p => p.Owner)
            .Include(p => p.Pins)
            .ToListAsync(ct);

        if (userId != null)
        {
            var pinnedIds = prompts
                .Where(p => p.Pins.Any(pin => pin.UserId == userId))
                .Select(p => p.Id)
                .ToHashSet();

            return prompts
                .OrderByDescending(p => pinnedIds.Contains(p.Id))
                .ThenByDescending(p => p.AverageRating)
                .ThenByDescending(p => p.CreatedAt)
                .ToList();
        }

        return prompts
            .OrderByDescending(p => p.AverageRating)
            .ThenByDescending(p => p.CreatedAt)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<Prompt?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _db.Prompts
            .Include(p => p.Owner)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    /// <inheritdoc />
    public async Task<Prompt> CreateAsync(Prompt prompt, CancellationToken ct = default)
    {
        prompt.CreatedAt = DateTime.UtcNow;
        prompt.TitleDescriptionEmbedding = _embeddingService.GetEmbeddingBytes($"{prompt.Title} {prompt.Description}");
        _db.Prompts.Add(prompt);
        await _db.SaveChangesAsync(ct);
        return prompt;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(int id, string userId, string title, string description, string content, CancellationToken ct = default)
    {
        var prompt = await RequireAsync(id, ct);
        if (prompt.OwnerId != userId)
            throw new UnauthorizedAccessException($"User {userId} does not own prompt {id}.");
        prompt.Title = title;
        prompt.Description = description;
        prompt.Content = content;
        prompt.TitleDescriptionEmbedding = _embeddingService.GetEmbeddingBytes($"{title} {description}");
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int id, string userId, CancellationToken ct = default)
    {
        var prompt = await RequireAsync(id, ct);
        if (prompt.OwnerId != userId)
            throw new UnauthorizedAccessException($"User {userId} does not own prompt {id}.");
        _db.Prompts.Remove(prompt);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<RatingResult?> RateAsync(int promptId, string userId, int stars, CancellationToken ct = default)
    {
        if (stars < 1 || stars > 5)
            throw new ArgumentOutOfRangeException(nameof(stars), "Stars must be between 1 and 5.");

        var alreadyRated = await _db.UserPromptRatings
            .AnyAsync(r => r.UserId == userId && r.PromptId == promptId, ct);
        if (alreadyRated)
            return null;

        var prompt = await RequireAsync(promptId, ct);
        _db.UserPromptRatings.Add(new UserPromptRating { UserId = userId, PromptId = promptId, Stars = stars });
        prompt.RatingTotal += stars;
        prompt.RatingCount++;
        await _db.SaveChangesAsync(ct);

        return new RatingResult(Math.Round(prompt.AverageRating, 1), prompt.RatingCount);
    }

    /// <inheritdoc />
    public async Task<bool> TogglePinAsync(int promptId, string userId, CancellationToken ct = default)
    {
        // Validate the prompt exists before touching pins
        await RequireAsync(promptId, ct);

        var existing = await _db.UserPromptPins
            .FirstOrDefaultAsync(p => p.UserId == userId && p.PromptId == promptId, ct);

        if (existing != null)
        {
            _db.UserPromptPins.Remove(existing);
            await _db.SaveChangesAsync(ct);
            return false;
        }

        _db.UserPromptPins.Add(new UserPromptPin { UserId = userId, PromptId = promptId });
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Prompt>> SearchAsync(string query, string? userId = null, CancellationToken ct = default)
    {
        var queryEmbedding = _embeddingService.GetEmbeddingBytes(query);

        var prompts = await _db.Prompts
            .Include(p => p.Owner)
            .Include(p => p.Pins)
            .Where(p => p.TitleDescriptionEmbedding != null)
            .ToListAsync(ct);

        var scored = prompts
            .Select(p => (Prompt: p, Score: _embeddingService.Similarity(queryEmbedding, p.TitleDescriptionEmbedding!)))
            .Where(x => x.Score >= _similarityThreshold)
            .ToList();

        if (userId != null)
        {
            var pinnedIds = scored
                .Select(x => x.Prompt)
                .Where(p => p.Pins.Any(pin => pin.UserId == userId))
                .Select(p => p.Id)
                .ToHashSet();

            return scored
                .OrderByDescending(x => pinnedIds.Contains(x.Prompt.Id))
                .ThenByDescending(x => x.Score)
                .Select(x => x.Prompt)
                .ToList();
        }

        return scored
            .OrderByDescending(x => x.Score)
            .Select(x => x.Prompt)
            .ToList();
    }

    /// <summary>
    /// Fetches a prompt by <paramref name="id"/> or throws <see cref="KeyNotFoundException"/>.
    /// </summary>
    /// <param name="id">The primary key to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The found <see cref="Prompt"/>.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no prompt with <paramref name="id"/> exists.</exception>
    private async Task<Prompt> RequireAsync(int id, CancellationToken ct)
    {
        return await _db.Prompts.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"Prompt with id {id} was not found.");
    }
}
