using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using PromptBank.Data;
using PromptBank.Models;
using PromptBank.Services;

namespace PromptBank.UnitTests.Services;

/// <summary>
/// Unit tests for <see cref="PromptService"/>.
/// Each test uses an isolated SQLite in-memory database so that SQL translation and 
/// query behaviour are tested faithfully against real SQLite semantics.
/// </summary>
public class PromptServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _ctx;
    private readonly PromptService _svc;
    private readonly Mock<IEmbeddingService> _mockEmbedding;

    // A reusable fake user ID for tests that need an owner
    private const string UserId1 = "user-1";
    private const string UserId2 = "user-2";

    // Fixed byte arrays used to represent stored embeddings in search tests
    private static readonly byte[] EmbeddingA = new byte[] { 1, 0, 0, 0 };
    private static readonly byte[] EmbeddingB = new byte[] { 0, 1, 0, 0 };

    /// <summary>Initialises the test with a fresh in-memory SQLite database.</summary>
    public PromptServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _ctx = new AppDbContext(options);
        _ctx.Database.EnsureCreated();

        // Seed test users to satisfy FK constraints on Prompt.OwnerId
        _ctx.Users.AddRange(
            new ApplicationUser { Id = UserId1, UserName = "user1", NormalizedUserName = "USER1", SecurityStamp = Guid.NewGuid().ToString() },
            new ApplicationUser { Id = UserId2, UserName = "user2", NormalizedUserName = "USER2", SecurityStamp = Guid.NewGuid().ToString() }
        );
        _ctx.SaveChanges();

        _mockEmbedding = new Mock<IEmbeddingService>();
        // Default: GetEmbeddingBytes returns EmbeddingA; Similarity returns 0 (overridden per test)
        _mockEmbedding.Setup(e => e.GetEmbeddingBytes(It.IsAny<string>())).Returns(EmbeddingA);
        _mockEmbedding.Setup(e => e.Similarity(It.IsAny<byte[]>(), It.IsAny<byte[]>())).Returns(0f);

        var searchOptions = Options.Create(new SearchOptions { SimilarityThreshold = 0.25f });
        _svc = new PromptService(_ctx, _mockEmbedding.Object, searchOptions);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _ctx.Dispose();
        _connection.Dispose();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>Convenience factory for a minimal valid <see cref="Prompt"/>.</summary>
    private static Prompt MakePrompt(string title = "T", string content = "C",
        string description = "D",
        string ownerId = UserId1, int ratingTotal = 0, int ratingCount = 0,
        DateTime? createdAt = null)
        => new Prompt
        {
            Title = title,
            Description = description,
            Content = content,
            OwnerId = ownerId,
            RatingTotal = ratingTotal,
            RatingCount = ratingCount,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };

    // -----------------------------------------------------------------------
    // GetAllAsync
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that when no userId is provided, prompts are sorted by rating then date.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_SortsByAverageRatingDescending_WhenNoUserId()
    {
        _ctx.Prompts.AddRange(
            MakePrompt("Low", ratingTotal: 3, ratingCount: 3),    // avg 1.0
            MakePrompt("High", ratingTotal: 15, ratingCount: 3)); // avg 5.0
        await _ctx.SaveChangesAsync();

        var result = await _svc.GetAllAsync();

        Assert.Equal("High", result[0].Title);
        Assert.Equal("Low", result[1].Title);
    }

    /// <summary>
    /// Verifies that when a userId is provided, prompts pinned by that user appear first.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_ReturnsPinnedForUserFirst_WhenUserIdProvided()
    {
        var unpinned = MakePrompt("Unpinned", ratingTotal: 50, ratingCount: 10); // high rating
        var pinned   = MakePrompt("Pinned",   ratingTotal: 0,  ratingCount: 0);  // no rating
        _ctx.Prompts.AddRange(unpinned, pinned);
        await _ctx.SaveChangesAsync();

        // Pin the lower-rated prompt for UserId1
        _ctx.UserPromptPins.Add(new UserPromptPin { UserId = UserId1, PromptId = pinned.Id });
        await _ctx.SaveChangesAsync();

        var result = await _svc.GetAllAsync(UserId1);

        Assert.Equal(2, result.Count);
        Assert.Equal("Pinned", result[0].Title);
        Assert.Equal("Unpinned", result[1].Title);
    }

    /// <summary>
    /// Verifies that when average ratings are equal, prompts are ordered
    /// by <see cref="Prompt.CreatedAt"/> descending so the newest appears first.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_SortsByDateDescending_WhenRatingEqual()
    {
        var older = DateTime.UtcNow.AddDays(-10);
        var newer = DateTime.UtcNow;

        _ctx.Prompts.AddRange(
            MakePrompt("Older", createdAt: older),
            MakePrompt("Newer", createdAt: newer));
        await _ctx.SaveChangesAsync();

        var result = await _svc.GetAllAsync();

        Assert.Equal("Newer", result[0].Title);
        Assert.Equal("Older", result[1].Title);
    }

    /// <summary>
    /// Verifies that <see cref="PromptService.GetAllAsync"/> returns an empty list
    /// when no prompts exist in the database.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_ReturnsEmpty_WhenNoPrompts()
    {
        var result = await _svc.GetAllAsync();
        Assert.Empty(result);
    }

    // -----------------------------------------------------------------------
    // GetByIdAsync
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that an existing prompt is returned when searching by its primary key.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_ReturnsPrompt_WhenExists()
    {
        var prompt = MakePrompt("FindMe");
        _ctx.Prompts.Add(prompt);
        await _ctx.SaveChangesAsync();

        var result = await _svc.GetByIdAsync(prompt.Id);

        Assert.NotNull(result);
        Assert.Equal("FindMe", result.Title);
    }

    /// <summary>
    /// Verifies that <c>null</c> is returned when no prompt matches the requested id.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _svc.GetByIdAsync(9999);
        Assert.Null(result);
    }

    // -----------------------------------------------------------------------
    // CreateAsync
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that <see cref="PromptService.CreateAsync"/> overwrites
    /// <see cref="Prompt.CreatedAt"/> with the current UTC time.
    /// </summary>
    [Fact]
    public async Task CreateAsync_SetsCreatedAtToUtcNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);

        var prompt = MakePrompt();
        prompt.CreatedAt = DateTime.MinValue; // intentionally wrong value
        var saved = await _svc.CreateAsync(prompt);

        Assert.True(saved.CreatedAt >= before,
            "CreatedAt should be set to approximately UtcNow by CreateAsync.");
        Assert.Equal(DateTimeKind.Utc, saved.CreatedAt.Kind);
    }

    /// <summary>
    /// Verifies that all fields supplied to <see cref="PromptService.CreateAsync"/>
    /// are persisted and readable via a second round-trip.
    /// </summary>
    [Fact]
    public async Task CreateAsync_PersistsAllFields()
    {
        var input = new Prompt
        {
            Title = "My Title",
            Description = "My Description",
            Content = "My Content",
            OwnerId = UserId1
        };

        var saved = await _svc.CreateAsync(input);

        var fetched = await _ctx.Prompts.FindAsync(saved.Id);
        Assert.NotNull(fetched);
        Assert.Equal("My Title", fetched.Title);
        Assert.Equal("My Description", fetched.Description);
        Assert.Equal("My Content", fetched.Content);
        Assert.Equal(UserId1, fetched.OwnerId);
        Assert.NotEqual(0, fetched.Id);
    }

    // -----------------------------------------------------------------------
    // UpdateAsync
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that title, description, and content are replaced when UpdateAsync is called by the owner.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_UpdatesTitleDescriptionAndContent_WhenOwner()
    {
        var prompt = MakePrompt("Old Title", "Old Content", description: "Old Description");
        _ctx.Prompts.Add(prompt);
        await _ctx.SaveChangesAsync();

        await _svc.UpdateAsync(prompt.Id, UserId1, "New Title", "New Description", "New Content");

        var updated = await _ctx.Prompts.FindAsync(prompt.Id);
        Assert.NotNull(updated);
        Assert.Equal("New Title", updated.Title);
        Assert.Equal("New Description", updated.Description);
        Assert.Equal("New Content", updated.Content);
    }

    /// <summary>
    /// Verifies that <see cref="PromptService.UpdateAsync"/> throws
    /// <see cref="KeyNotFoundException"/> when the requested prompt id does not exist.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_Throws_WhenNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _svc.UpdateAsync(9999, UserId1, "T", "D", "C"));
    }

    /// <summary>
    /// Verifies that <see cref="PromptService.UpdateAsync"/> throws
    /// <see cref="UnauthorizedAccessException"/> when the user is not the owner.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_Throws_WhenNotOwner()
    {
        var prompt = MakePrompt(ownerId: UserId1);
        _ctx.Prompts.Add(prompt);
        await _ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _svc.UpdateAsync(prompt.Id, UserId2, "T", "D", "C"));
    }

    // -----------------------------------------------------------------------
    // DeleteAsync
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that a prompt is removed from the database after DeleteAsync is called by the owner.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_RemovesPrompt_WhenOwner()
    {
        var prompt = MakePrompt("ToDelete");
        _ctx.Prompts.Add(prompt);
        await _ctx.SaveChangesAsync();

        await _svc.DeleteAsync(prompt.Id, UserId1);

        var fetched = await _ctx.Prompts.FindAsync(prompt.Id);
        Assert.Null(fetched);
    }

    /// <summary>
    /// Verifies that <see cref="PromptService.DeleteAsync"/> throws
    /// <see cref="KeyNotFoundException"/> when the requested prompt id does not exist.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_Throws_WhenNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _svc.DeleteAsync(9999, UserId1));
    }

    /// <summary>
    /// Verifies that <see cref="PromptService.DeleteAsync"/> throws
    /// <see cref="UnauthorizedAccessException"/> when the user is not the owner.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_Throws_WhenNotOwner()
    {
        var prompt = MakePrompt(ownerId: UserId1);
        _ctx.Prompts.Add(prompt);
        await _ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _svc.DeleteAsync(prompt.Id, UserId2));
    }

    // -----------------------------------------------------------------------
    // Description field
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that a prompt with a description is persisted and the description is readable.
    /// </summary>
    [Fact]
    public async Task CreateAsync_PersistsDescription()
    {
        var prompt = new Prompt
        {
            Title = "T",
            Description = "Explains how to do X in Y context.",
            Content = "C",
            OwnerId = UserId1
        };

        var saved = await _svc.CreateAsync(prompt);

        var fetched = await _ctx.Prompts.FindAsync(saved.Id);
        Assert.NotNull(fetched);
        Assert.Equal("Explains how to do X in Y context.", fetched.Description);
    }

    /// <summary>
    /// Verifies that a Description exceeding 500 characters fails DataAnnotation validation.
    /// </summary>
    [Fact]
    public void Description_FailsValidation_WhenExceeds500Characters()
    {
        var prompt = new Prompt
        {
            Title = "T",
            Description = new string('x', 501),
            Content = "C",
            OwnerId = UserId1
        };

        var results = new System.Collections.Generic.List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var ctx = new System.ComponentModel.DataAnnotations.ValidationContext(prompt) { MemberName = nameof(Prompt.Description) };
        bool isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateProperty(
            prompt.Description, ctx, results);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(Prompt.Description)));
    }

    /// <summary>
    /// Verifies that an empty Description fails required validation.
    /// </summary>
    [Fact]
    public void Description_FailsValidation_WhenEmpty()
    {
        var prompt = new Prompt
        {
            Title = "T",
            Description = "",
            Content = "C",
            OwnerId = UserId1
        };

        var results = new System.Collections.Generic.List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var ctx = new System.ComponentModel.DataAnnotations.ValidationContext(prompt) { MemberName = nameof(Prompt.Description) };
        bool isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateProperty(
            prompt.Description, ctx, results);

        Assert.False(isValid);
    }

    // -----------------------------------------------------------------------
    // RateAsync
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that a rating vote increments RatingTotal and RatingCount correctly.
    /// </summary>
    [Fact]
    public async Task RateAsync_AccumulatesRatingTotalAndCount()
    {
        var prompt = MakePrompt();
        _ctx.Prompts.Add(prompt);
        await _ctx.SaveChangesAsync();

        await _svc.RateAsync(prompt.Id, UserId1, 4);
        await _svc.RateAsync(prompt.Id, UserId2, 2);

        var updated = await _ctx.Prompts.FindAsync(prompt.Id);
        Assert.NotNull(updated);
        Assert.Equal(6, updated.RatingTotal);
        Assert.Equal(2, updated.RatingCount);
    }

    /// <summary>
    /// Verifies that the <see cref="RatingResult"/> returned by RateAsync carries the correct average.
    /// </summary>
    [Fact]
    public async Task RateAsync_ReturnsCorrectAverage()
    {
        var prompt = MakePrompt(ratingTotal: 8, ratingCount: 2); // avg 4.0
        _ctx.Prompts.Add(prompt);
        await _ctx.SaveChangesAsync();

        // Add a third vote of 1 star → total=9, count=3 → avg=3.0
        var result = await _svc.RateAsync(prompt.Id, UserId1, 1);

        Assert.NotNull(result);
        Assert.Equal(3.0, result.Average);
        Assert.Equal(3, result.Count);
    }

    /// <summary>
    /// Verifies that a user cannot rate the same prompt twice (returns null).
    /// </summary>
    [Fact]
    public async Task RateAsync_ReturnsNull_WhenAlreadyRated()
    {
        var prompt = MakePrompt();
        _ctx.Prompts.Add(prompt);
        await _ctx.SaveChangesAsync();

        await _svc.RateAsync(prompt.Id, UserId1, 4);
        var result = await _svc.RateAsync(prompt.Id, UserId1, 5); // second vote

        Assert.Null(result);
    }

    /// <summary>
    /// Verifies that RateAsync throws <see cref="ArgumentOutOfRangeException"/> when stars is 0.
    /// </summary>
    [Fact]
    public async Task RateAsync_Throws_WhenStarsIsZero()
    {
        var prompt = MakePrompt();
        _ctx.Prompts.Add(prompt);
        await _ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _svc.RateAsync(prompt.Id, UserId1, 0));
    }

    /// <summary>
    /// Verifies that RateAsync throws <see cref="ArgumentOutOfRangeException"/> when stars is 6.
    /// </summary>
    [Fact]
    public async Task RateAsync_Throws_WhenStarsIsSix()
    {
        var prompt = MakePrompt();
        _ctx.Prompts.Add(prompt);
        await _ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _svc.RateAsync(prompt.Id, UserId1, 6));
    }

    /// <summary>
    /// Verifies that RateAsync throws <see cref="KeyNotFoundException"/> when the prompt does not exist.
    /// </summary>
    [Fact]
    public async Task RateAsync_Throws_WhenPromptNotFound()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _svc.RateAsync(9999, UserId1, 0)); // stars=0 checked first

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _svc.RateAsync(9999, UserId1, 3)); // valid stars, missing prompt
    }

    // -----------------------------------------------------------------------
    // TogglePinAsync
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that calling TogglePinAsync on an unpinned prompt pins it and returns true.
    /// </summary>
    [Fact]
    public async Task TogglePinAsync_PinsPrompt_WhenNotPinned()
    {
        var prompt = MakePrompt();
        _ctx.Prompts.Add(prompt);
        await _ctx.SaveChangesAsync();

        var result = await _svc.TogglePinAsync(prompt.Id, UserId1);

        Assert.True(result, "TogglePinAsync should return true after pinning.");
        var pin = await _ctx.UserPromptPins
            .FirstOrDefaultAsync(p => p.UserId == UserId1 && p.PromptId == prompt.Id);
        Assert.NotNull(pin);
    }

    /// <summary>
    /// Verifies that calling TogglePinAsync on a pinned prompt unpins it and returns false.
    /// </summary>
    [Fact]
    public async Task TogglePinAsync_UnpinsPrompt_WhenAlreadyPinned()
    {
        var prompt = MakePrompt();
        _ctx.Prompts.Add(prompt);
        await _ctx.SaveChangesAsync();

        // Pin first
        _ctx.UserPromptPins.Add(new UserPromptPin { UserId = UserId1, PromptId = prompt.Id });
        await _ctx.SaveChangesAsync();

        var result = await _svc.TogglePinAsync(prompt.Id, UserId1);

        Assert.False(result, "TogglePinAsync should return false after unpinning.");
        var pin = await _ctx.UserPromptPins
            .FirstOrDefaultAsync(p => p.UserId == UserId1 && p.PromptId == prompt.Id);
        Assert.Null(pin);
    }

    /// <summary>
    /// Verifies that TogglePinAsync throws <see cref="KeyNotFoundException"/> when the prompt does not exist.
    /// </summary>
    [Fact]
    public async Task TogglePinAsync_Throws_WhenNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _svc.TogglePinAsync(9999, UserId1));
    }

    /// <summary>
    /// Verifies that two users can independently pin the same prompt.
    /// </summary>
    [Fact]
    public async Task TogglePinAsync_AllowsDifferentUsersToPinSamePrompt()
    {
        var prompt = MakePrompt();
        _ctx.Prompts.Add(prompt);
        await _ctx.SaveChangesAsync();

        var result1 = await _svc.TogglePinAsync(prompt.Id, UserId1);
        var result2 = await _svc.TogglePinAsync(prompt.Id, UserId2);

        Assert.True(result1);
        Assert.True(result2);

        var pins = await _ctx.UserPromptPins
            .Where(p => p.PromptId == prompt.Id)
            .ToListAsync();
        Assert.Equal(2, pins.Count);
    }

    // -----------------------------------------------------------------------
    // SearchAsync
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that prompts whose stored embedding scores above the threshold are returned.
    /// </summary>
    [Fact]
    public async Task SearchAsync_ReturnsMatchingPrompts_WhenSimilarityAboveThreshold()
    {
        var p1 = MakePrompt("Matching", description: "Relevant");
        var p2 = MakePrompt("Other", description: "Irrelevant");
        p1.TitleDescriptionEmbedding = EmbeddingA;
        p2.TitleDescriptionEmbedding = EmbeddingB;
        _ctx.Prompts.AddRange(p1, p2);
        await _ctx.SaveChangesAsync();

        // Mock: query embedding = EmbeddingA; p1 scores high, p2 scores low
        _mockEmbedding.Setup(e => e.GetEmbeddingBytes("find me")).Returns(EmbeddingA);
        _mockEmbedding.Setup(e => e.Similarity(EmbeddingA, EmbeddingA)).Returns(0.9f);
        _mockEmbedding.Setup(e => e.Similarity(EmbeddingA, EmbeddingB)).Returns(0.1f);

        var result = await _svc.SearchAsync("find me");

        Assert.Single(result);
        Assert.Equal("Matching", result[0].Title);
    }

    /// <summary>
    /// Verifies that an unrelated query returns an empty result set.
    /// </summary>
    [Fact]
    public async Task SearchAsync_ReturnsEmpty_WhenNoPromptsMeetThreshold()
    {
        var p1 = MakePrompt("Something");
        p1.TitleDescriptionEmbedding = EmbeddingA;
        _ctx.Prompts.Add(p1);
        await _ctx.SaveChangesAsync();

        // Similarity always below threshold
        _mockEmbedding.Setup(e => e.Similarity(It.IsAny<byte[]>(), It.IsAny<byte[]>())).Returns(0.05f);

        var result = await _svc.SearchAsync("completely unrelated query");

        Assert.Empty(result);
    }

    /// <summary>
    /// Verifies that pinned prompts appear first in search results when a userId is provided.
    /// </summary>
    [Fact]
    public async Task SearchAsync_ReturnsPinnedPromptsFirst_WhenUserIdProvided()
    {
        var pinned = MakePrompt("Pinned");
        var unpinned = MakePrompt("Unpinned");
        pinned.TitleDescriptionEmbedding = EmbeddingA;
        unpinned.TitleDescriptionEmbedding = EmbeddingB;
        _ctx.Prompts.AddRange(pinned, unpinned);
        await _ctx.SaveChangesAsync();

        _ctx.UserPromptPins.Add(new UserPromptPin { UserId = UserId1, PromptId = pinned.Id });
        await _ctx.SaveChangesAsync();

        // Both prompts score above threshold, but unpinned has higher similarity score
        _mockEmbedding.Setup(e => e.Similarity(EmbeddingA, EmbeddingA)).Returns(0.6f); // pinned score
        _mockEmbedding.Setup(e => e.Similarity(EmbeddingA, EmbeddingB)).Returns(0.9f); // unpinned score (higher)

        var result = await _svc.SearchAsync("query", UserId1);

        Assert.Equal(2, result.Count);
        Assert.Equal("Pinned", result[0].Title);   // pinned wins despite lower score
        Assert.Equal("Unpinned", result[1].Title);
    }

    /// <summary>
    /// Verifies that prompts with null embeddings are excluded from search results.
    /// </summary>
    [Fact]
    public async Task SearchAsync_ExcludesPrompts_WhenEmbeddingIsNull()
    {
        var withEmbedding = MakePrompt("HasEmbedding");
        var withoutEmbedding = MakePrompt("NoEmbedding");
        withEmbedding.TitleDescriptionEmbedding = EmbeddingA;
        withoutEmbedding.TitleDescriptionEmbedding = null;
        _ctx.Prompts.AddRange(withEmbedding, withoutEmbedding);
        await _ctx.SaveChangesAsync();

        _mockEmbedding.Setup(e => e.Similarity(EmbeddingA, EmbeddingA)).Returns(0.9f);

        var result = await _svc.SearchAsync("some query");

        Assert.Single(result);
        Assert.Equal("HasEmbedding", result[0].Title);
    }
}
