using PromptBank.Models;
using PromptBank.Tests.Infrastructure;

namespace PromptBank.Tests.Tests;

/// <summary>
/// E2E tests for global navigation elements shared across all pages of the Prompt Bank.
/// Verifies that the navbar brand link and the primary CTA button route users to the
/// correct pages.
/// </summary>
[Collection("Playwright")]
public sealed class NavigationTests : E2ETestBase
{
    /// <summary>
    /// Initialises the test class with the shared Playwright browser fixture.
    /// </summary>
    /// <param name="playwright">The Playwright fixture provided by the xUnit collection.</param>
    public NavigationTests(PlaywrightFixture playwright) : base(playwright) { }

    /// <summary>
    /// Verifies that clicking the "Prompt Bank 📚" brand link in the navbar returns the
    /// user to the Index page from any other page (here: from the Create page).
    /// </summary>
    [Fact]
    public async Task BrandLink_NavigatesToIndex_FromAnyPage()
    {
        // Arrange – log in and navigate to the Create page.
        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");
        await Page.GotoAsync($"{BaseUrl}/Prompts/Create");
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/Prompts/Create"));

        // Act – click the navbar brand.
        await Page.Locator(".navbar-brand").ClickAsync();

        // Assert – should land on the root Index page.
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/$"));
    }

    /// <summary>
    /// Verifies that clicking the "Add Prompt" button on the Index page (visible to
    /// authenticated users) navigates to the Create Prompt page.
    /// </summary>
    [Fact]
    public async Task AddPromptButton_NavigatesToCreate_WhenAuthenticated()
    {
        // Arrange – log in; the "Add Prompt" link renders on the Index page for auth users.
        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");

        // Act – click the "Add Prompt" link in the page header.
        await Page.Locator("a[href='/Prompts/Create']").First.ClickAsync();

        // Assert
        await Expect(Page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex("/Prompts/Create"));
    }
}
