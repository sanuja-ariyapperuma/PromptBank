using PromptBank.Models;
using PromptBank.Tests.Infrastructure;

namespace PromptBank.Tests.Tests;

/// <summary>
/// E2E tests for the copy-to-clipboard feature on the Index page.
/// Each test seeds a single prompt, navigates to <c>/</c>, and exercises the
/// <c>.btn-copy</c> button behaviour.
/// </summary>
/// <remarks>
/// The browser context is created with <c>clipboard-read</c> and <c>clipboard-write</c>
/// permissions so that <c>navigator.clipboard.readText()</c> can be called from JavaScript
/// within the test page.
/// </remarks>
[Collection("Playwright")]
public sealed class CopyTests : E2ETestBase
{
    /// <summary>
    /// Initialises the test class with the shared Playwright browser fixture.
    /// </summary>
    /// <param name="playwright">The Playwright fixture provided by the xUnit collection.</param>
    public CopyTests(PlaywrightFixture playwright) : base(playwright) { }

    /// <summary>
    /// Verifies that clicking the Copy button writes the prompt's content to the system clipboard.
    /// </summary>
    [Fact]
    public async Task CopyButton_WritesContentToClipboard_WhenClicked()
    {
        // Arrange
        const string expectedContent = "Explain the following code step by step.";
        await SeedAsync(new Prompt
        {
            Id = 1,
            Title  = "Copy Test Prompt",
            Content = expectedContent,
            OwnerId = TestUserId
        });
        await Page.GotoAsync($"{BaseUrl}/");

        // Act – click the copy button on the single visible card.
        await Page.Locator(".btn-copy").First.ClickAsync();

        // Assert – the clipboard must contain the prompt content.
        var clipboardText = await Page.EvaluateAsync<string>("() => navigator.clipboard.readText()");
        Assert.Equal(expectedContent, clipboardText);
    }

    /// <summary>
    /// Verifies that after clicking the Copy button the button label changes to "Copied"
    /// to give the user immediate visual feedback.
    /// </summary>
    [Fact]
    public async Task CopyButton_ShowsCopiedFeedback_AfterClick()
    {
        // Arrange
        await SeedAsync(new Prompt
        {
            Id = 1,
            Title   = "Copy Feedback Prompt",
            Content = "Some content.",
            OwnerId = TestUserId
        });
        await Page.GotoAsync($"{BaseUrl}/");

        // Act
        await Page.Locator(".btn-copy").First.ClickAsync();

        // Assert – button text must contain "Copied" shortly after the click.
        await Expect(Page.Locator(".btn-copy").First).ToContainTextAsync("Copied");
    }

    /// <summary>
    /// Verifies that the Copy button automatically reverts its label back to "Copy"
    /// approximately 2 seconds after the copy action.
    /// </summary>
    [Fact]
    public async Task CopyButton_RestoresOriginalText_AfterTimeout()
    {
        // Arrange
        await SeedAsync(new Prompt
        {
            Id = 1,
            Title   = "Restore Label Prompt",
            Content = "Some content.",
            OwnerId = TestUserId
        });
        await Page.GotoAsync($"{BaseUrl}/");
        var copyBtn = Page.Locator(".btn-copy").First;

        // Act – click and wait for the 2-second restore timeout (site.js uses 2000 ms).
        await copyBtn.ClickAsync();
        await Expect(copyBtn).ToContainTextAsync("Copied"); // ensure feedback appeared first
        await Page.WaitForTimeoutAsync(2500);               // wait past the 2 000 ms window

        // Assert – the label must no longer contain "Copied".
        var buttonText = await copyBtn.TextContentAsync();
        Assert.DoesNotContain("Copied", buttonText ?? string.Empty);
    }
}
