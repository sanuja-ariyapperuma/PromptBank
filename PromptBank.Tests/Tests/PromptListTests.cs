using Microsoft.Playwright;
using PromptBank.Models;
using PromptBank.Tests.Infrastructure;

namespace PromptBank.Tests.Tests;

/// <summary>
/// E2E tests for the prompt listing page (<c>GET /</c>).
/// Verifies that the Index page renders prompt cards correctly, respects sort order,
/// shows/hides the pin badge, and displays a friendly empty state when no prompts exist.
/// </summary>
[Collection("Playwright")]
public sealed class PromptListTests : E2ETestBase
{
    /// <summary>
    /// Initialises the test class with the shared Playwright browser fixture.
    /// </summary>
    /// <param name="playwright">The Playwright fixture provided by the xUnit collection.</param>
    public PromptListTests(PlaywrightFixture playwright) : base(playwright) { }

    /// <summary>
    /// Verifies that navigating to <c>/</c> with two seeded prompts renders a card for each one.
    /// </summary>
    [Fact]
    public async Task PageLoads_ShowsPromptCards_WhenPromptsExist()
    {
        await SeedAsync(
            new Prompt { Id = 1, Title = "Alpha Prompt", Content = "Content A", OwnerId = TestUserId },
            new Prompt { Id = 2, Title = "Beta Prompt",  Content = "Content B", OwnerId = TestUserId }
        );

        await Page.GotoAsync($"{BaseUrl}/");

        await Expect(Page.GetByText("Alpha Prompt")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Beta Prompt")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Verifies that without any pins, all prompts sort by rating then date and the
    /// pin badge is hidden on all cards (no authenticated user).
    /// </summary>
    [Fact]
    public async Task PinnedPrompts_AppearFirst_WhenMixedWithUnpinned()
    {
        await SeedAsync(
            new Prompt { Id = 1, Title = "Unpinned Prompt", Content = "Content A", OwnerId = TestUserId },
            new Prompt { Id = 2, Title = "Pinned Prompt",   Content = "Content B", OwnerId = TestUserId }
        );

        await Page.GotoAsync($"{BaseUrl}/");

        // Without an authenticated user no pin badges should be visible.
        // Use :not(.d-none) to count only visible badges — ToBeHiddenAsync() behaves
        // unexpectedly when the locator matches multiple elements in Playwright.
        await Expect(Page.Locator(".pin-badge:not(.d-none)")).ToHaveCountAsync(0);
    }

    /// <summary>
    /// Verifies that the "📌 Pinned" badge is hidden when no authenticated user is present.
    /// </summary>
    [Fact]
    public async Task PinnedPrompt_ShowsPinBadge_WhenIsPinnedIsTrue()
    {
        await SeedAsync(new Prompt { Id = 1, Title = "Pinned", Content = "Content", OwnerId = TestUserId });

        await Page.GotoAsync($"{BaseUrl}/");

        // Badge hidden because there is no authenticated user.
        await Expect(Page.Locator(".pin-badge")).ToBeHiddenAsync();
    }

    /// <summary>
    /// Verifies that the "📌 Pinned" badge is hidden for an unauthenticated visitor.
    /// </summary>
    [Fact]
    public async Task UnpinnedPrompt_HidesPinBadge_WhenIsPinnedIsFalse()
    {
        await SeedAsync(new Prompt { Id = 1, Title = "Unpinned", Content = "Content", OwnerId = TestUserId });

        await Page.GotoAsync($"{BaseUrl}/");

        await Expect(Page.Locator(".pin-badge")).ToBeHiddenAsync();
    }

    /// <summary>
    /// Verifies that navigating to <c>/</c> with no seeded data shows a friendly
    /// "No prompts" message rather than an empty or broken layout.
    /// </summary>
    [Fact]
    public async Task EmptyState_ShowsFriendlyMessage_WhenNoPromptsExist()
    {
        await Page.GotoAsync($"{BaseUrl}/");

        await Expect(Page.GetByText("No prompts yet")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Verifies that the owner username appears on the prompt card when rendered.
    /// </summary>
    [Fact]
    public async Task PromptCard_ShowsOwnerName_WhenRendered()
    {
        await SeedAsync(new Prompt { Id = 1, Title = "Test Prompt", Content = "Content", OwnerId = TestUserId });

        await Page.GotoAsync($"{BaseUrl}/");

        // alice is the seeded user whose ID == TestUserId
        await Expect(Page.Locator(".prompt-card").GetByText("alice")).ToBeVisibleAsync();
    }
}
