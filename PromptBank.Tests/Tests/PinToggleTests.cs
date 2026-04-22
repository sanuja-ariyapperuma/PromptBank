using PromptBank.Models;
using PromptBank.Tests.Infrastructure;

namespace PromptBank.Tests.Tests;

/// <summary>
/// E2E tests for the pin/unpin AJAX toggle on the Index page.
/// Verifies that clicking the <c>.btn-pin</c> button updates the badge visibility,
/// the button text, and does so without triggering a full-page reload.
/// </summary>
[Collection("Playwright")]
public sealed class PinToggleTests : E2ETestBase
{
    /// <summary>
    /// Initialises the test class with the shared Playwright browser fixture.
    /// </summary>
    /// <param name="playwright">The Playwright fixture provided by the xUnit collection.</param>
    public PinToggleTests(PlaywrightFixture playwright) : base(playwright) { }

    /// <summary>
    /// Verifies that clicking the Pin button on an unpinned prompt makes the
    /// "📌 Pinned" badge visible on that card.
    /// </summary>
    [Fact]
    public async Task PinToggle_PinsUnpinnedPrompt_BadgeBecomesVisible()
    {
        await SeedAsync(new Prompt
        {
            Id = 1, Title = "Unpin Me", Content = "Content", OwnerId = TestUserId
        });
        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");

        var card   = Page.Locator(".prompt-card").First;
        var badge  = card.Locator(".pin-badge");

        await Expect(badge).ToBeHiddenAsync();

        await card.Locator(".btn-pin").ClickAsync();

        await Expect(badge).ToBeVisibleAsync();
    }

    /// <summary>
    /// Verifies that clicking the Unpin button on a pinned prompt hides the
    /// "📌 Pinned" badge on that card.
    /// </summary>
    [Fact]
    public async Task PinToggle_UnpinsPinnedPrompt_BadgeBecomesHidden()
    {
        await SeedAsync(new Prompt
        {
            Id = 1, Title = "Already Pinned", Content = "Content", OwnerId = TestUserId
        });
        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");

        var card  = Page.Locator(".prompt-card").First;
        var badge = card.Locator(".pin-badge");

        await card.Locator(".btn-pin").ClickAsync();
        await Expect(badge).ToBeVisibleAsync();

        await card.Locator(".btn-pin").ClickAsync();

        await Expect(badge).ToBeHiddenAsync();
    }

    /// <summary>
    /// Verifies that the pin button's text label cycles between "📍 Pin" and "📌 Unpin"
    /// with each successive click.
    /// </summary>
    [Fact]
    public async Task PinToggle_UpdatesButtonText_OnEachClick()
    {
        await SeedAsync(new Prompt
        {
            Id = 1, Title = "Toggle Text Test", Content = "Content", OwnerId = TestUserId
        });
        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");

        var pinBtn = Page.Locator(".btn-pin").First;

        await Expect(pinBtn).ToContainTextAsync("Pin");

        await pinBtn.ClickAsync();

        await Expect(pinBtn).ToContainTextAsync("Unpin");

        await pinBtn.ClickAsync();

        await Expect(pinBtn).ToContainTextAsync("Pin");
    }

    /// <summary>
    /// Verifies that the pin toggle does not cause a full-page navigation.
    /// Uses a <c>sessionStorage</c> marker that would be erased by a hard reload.
    /// </summary>
    [Fact]
    public async Task PinToggle_NoFullPageReload_AfterToggle()
    {
        await SeedAsync(new Prompt
        {
            Id = 1, Title = "No Reload Pin Test", Content = "Content", OwnerId = TestUserId
        });
        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");

        await Page.EvaluateAsync("() => sessionStorage.setItem('pin-probe', 'intact')");

        var card   = Page.Locator(".prompt-card").First;
        var pinBtn = card.Locator(".btn-pin");
        await pinBtn.ClickAsync();

        await Expect(card.Locator(".pin-badge")).ToBeVisibleAsync();

        var marker = await Page.EvaluateAsync<string>("() => sessionStorage.getItem('pin-probe')");
        Assert.Equal("intact", marker);
    }
}
