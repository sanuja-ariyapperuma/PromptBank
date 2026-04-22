using PromptBank.Models;
using PromptBank.Tests.Infrastructure;

namespace PromptBank.Tests.Tests;

/// <summary>
/// E2E tests for the star-rating AJAX feature on the Index page.
/// Each test seeds prompts, navigates to <c>/</c>, clicks a star button, and asserts
/// that the UI updates correctly without a full-page navigation.
/// </summary>
[Collection("Playwright")]
public sealed class StarRatingTests : E2ETestBase
{
    /// <summary>
    /// Initialises the test class with the shared Playwright browser fixture.
    /// </summary>
    /// <param name="playwright">The Playwright fixture provided by the xUnit collection.</param>
    public StarRatingTests(PlaywrightFixture playwright) : base(playwright) { }

    /// <summary>
    /// Verifies that clicking star 4 on a prompt with no existing votes updates the
    /// <c>.avg-score</c> element to display "4.0".
    /// </summary>
    [Fact]
    public async Task ClickStar_UpdatesAverageScore_AfterVote()
    {
        // Arrange – seed a prompt with zero votes.
        await SeedAsync(new Prompt
        {
            Id = 1, Title = "Rate Me", Content = "Content", OwnerId = TestUserId,
            RatingTotal = 0, RatingCount = 0
        });
        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");

        var card = Page.Locator(".prompt-card").First;

        // Act – click star 4.
        await card.Locator(".star-btn[data-star='4']").ClickAsync();

        // Assert – avg-score must update to "4.0" (JS uses toFixed(1)).
        // Playwright's Expect retries until the condition holds or the timeout expires.
        await Expect(card.Locator(".avg-score")).ToContainTextAsync("4");
    }

    /// <summary>
    /// Verifies that clicking any star increments the vote count by one.
    /// </summary>
    [Fact]
    public async Task ClickStar_UpdatesVoteCount_AfterVote()
    {
        // Arrange
        await SeedAsync(new Prompt
        {
            Id = 1, Title = "Vote Count Test", Content = "Content", OwnerId = TestUserId,
            RatingTotal = 0, RatingCount = 0
        });
        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");

        var card = Page.Locator(".prompt-card").First;

        // Act
        await card.Locator(".star-btn[data-star='3']").ClickAsync();

        // Assert – vote count must show "1 vote" after the AJAX call resolves.
        await Expect(card.Locator(".vote-count")).ToContainTextAsync("1");
    }

    /// <summary>
    /// Verifies that clicking a star button does not cause a full-page navigation.
    /// Uses a <c>sessionStorage</c> marker that would be erased by a page reload as a
    /// reliable detection mechanism.
    /// </summary>
    [Fact]
    public async Task ClickStar_NoFullPageReload_AfterVote()
    {
        // Arrange
        await SeedAsync(new Prompt
        {
            Id = 1, Title = "No Reload Test", Content = "Content", OwnerId = TestUserId
        });
        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");

        // Plant a sessionStorage marker that would be cleared by a hard reload.
        await Page.EvaluateAsync("() => sessionStorage.setItem('reload-probe', 'intact')");

        // Act
        var card = Page.Locator(".prompt-card").First;
        await card.Locator(".star-btn[data-star='5']").ClickAsync();

        // Wait for the DOM to update, confirming the AJAX call completed.
        await Expect(card.Locator(".avg-score")).ToContainTextAsync("5");

        // Assert – marker must still be present, proving no page reload occurred.
        var marker = await Page.EvaluateAsync<string>("() => sessionStorage.getItem('reload-probe')");
        Assert.Equal("intact", marker);
    }

    /// <summary>
    /// Verifies that rating one card does not mutate the average score displayed on
    /// a different card on the same page.
    /// </summary>
    [Fact]
    public async Task ClickStar_OnlyUpdatesTargetCard_NotOtherCards()
    {
        // Arrange – seed two prompts, both with no votes.
        await SeedAsync(
            new Prompt { Id = 1, Title = "First Prompt",  Content = "Content A", OwnerId = TestUserId, RatingTotal = 0, RatingCount = 0, CreatedAt = DateTime.UtcNow },
            new Prompt { Id = 2, Title = "Second Prompt", Content = "Content B", OwnerId = TestUserId, RatingTotal = 0, RatingCount = 0, CreatedAt = DateTime.UtcNow.AddSeconds(-1) }
        );
        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");

        // Capture the second card's initial average-score text.
        var secondCard   = Page.Locator(".prompt-card").Nth(1);
        var initialScore = await secondCard.Locator(".avg-score").TextContentAsync();

        // Act – click star 5 on the FIRST card only.
        var firstCard = Page.Locator(".prompt-card").First;
        await firstCard.Locator(".star-btn[data-star='5']").ClickAsync();

        // Wait for the first card to update so we know the AJAX round-trip completed.
        await Expect(firstCard.Locator(".avg-score")).ToContainTextAsync("5");

        // Assert – the second card must not have changed.
        var afterScore = await secondCard.Locator(".avg-score").TextContentAsync();
        Assert.Equal(initialScore, afterScore);
    }
}
