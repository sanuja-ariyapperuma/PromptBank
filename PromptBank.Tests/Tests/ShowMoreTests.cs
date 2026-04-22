using PromptBank.Models;
using PromptBank.Tests.Infrastructure;

namespace PromptBank.Tests.Tests;

/// <summary>
/// E2E tests for the Show More / Show Less expandable content feature on the Index page.
/// Verifies that the toggle button is conditionally rendered and that it correctly expands
/// and collapses the <c>.prompt-content</c> pre element.
/// </summary>
[Collection("Playwright")]
public sealed class ShowMoreTests : E2ETestBase
{
    /// <summary>
    /// Initialises the test class with the shared Playwright browser fixture.
    /// </summary>
    /// <param name="playwright">The Playwright fixture provided by the xUnit collection.</param>
    public ShowMoreTests(PlaywrightFixture playwright) : base(playwright) { }

    /// <summary>
    /// Verifies that the "Show more" button is NOT rendered when the prompt content
    /// is shorter than the 200-character threshold.
    /// </summary>
    [Fact]
    public async Task ShortContent_NoShowMoreButton_WhenContentBelowThreshold()
    {
        // Arrange – content well under 200 characters.
        const string shortContent = "Short content.";
        await SeedAsync(new Prompt
        {
            Id = 1, Title = "Short Prompt", Content = shortContent, OwnerId = TestUserId
        });

        // Act
        await Page.GotoAsync($"{BaseUrl}/");

        // Assert – no .show-more-btn should exist in the DOM for short content.
        await Expect(Page.Locator(".show-more-btn")).ToHaveCountAsync(0);
    }

    /// <summary>
    /// Verifies that the "Show more" button IS rendered when the prompt content
    /// exceeds the 200-character threshold.
    /// </summary>
    [Fact]
    public async Task LongContent_ShowsShowMoreButton_WhenContentExceedsThreshold()
    {
        // Arrange – content over 200 characters.
        var longContent = new string('A', 201);
        await SeedAsync(new Prompt
        {
            Id = 1, Title = "Long Prompt", Content = longContent, OwnerId = TestUserId
        });

        // Act
        await Page.GotoAsync($"{BaseUrl}/");

        // Assert – the show-more button must be present and visible.
        await Expect(Page.Locator(".show-more-btn")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Verifies that clicking "Show more" expands the content block by setting the
    /// button's <c>aria-expanded</c> attribute to <c>"true"</c> and adding the
    /// <c>expanded</c> CSS class to the pre element.
    /// </summary>
    [Fact]
    public async Task ClickShowMore_ExpandsContent_WhenButtonClicked()
    {
        // Arrange
        var longContent = new string('B', 201);
        await SeedAsync(new Prompt
        {
            Id = 1, Title = "Expand Test", Content = longContent, OwnerId = TestUserId
        });
        await Page.GotoAsync($"{BaseUrl}/");

        var showMoreBtn = Page.Locator(".show-more-btn").First;
        var contentPre  = Page.Locator(".prompt-content").First;

        // Pre-condition: not yet expanded.
        await Expect(showMoreBtn).ToHaveAttributeAsync("aria-expanded", "false");

        // Act
        await showMoreBtn.ClickAsync();

        // Assert – aria-expanded must flip to "true" and the expanded class applied.
        await Expect(showMoreBtn).ToHaveAttributeAsync("aria-expanded", "true");
        await Expect(contentPre).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("expanded"));
    }

    /// <summary>
    /// Verifies that clicking "Show less" (after expanding) collapses the content block
    /// by resetting <c>aria-expanded</c> to <c>"false"</c> and removing the <c>expanded</c>
    /// CSS class from the pre element.
    /// </summary>
    [Fact]
    public async Task ClickShowLess_CollapsesContent_AfterExpanding()
    {
        // Arrange
        var longContent = new string('C', 201);
        await SeedAsync(new Prompt
        {
            Id = 1, Title = "Collapse Test", Content = longContent, OwnerId = TestUserId
        });
        await Page.GotoAsync($"{BaseUrl}/");

        var showMoreBtn = Page.Locator(".show-more-btn").First;
        var contentPre  = Page.Locator(".prompt-content").First;

        // First expand the content.
        await showMoreBtn.ClickAsync();
        await Expect(showMoreBtn).ToHaveAttributeAsync("aria-expanded", "true");

        // Act – click again to collapse ("Show less").
        await showMoreBtn.ClickAsync();

        // Assert – back to collapsed state.
        await Expect(showMoreBtn).ToHaveAttributeAsync("aria-expanded", "false");
        await Expect(contentPre).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex(@"\bexpanded\b"));
    }
}
