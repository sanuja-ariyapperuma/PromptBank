using Microsoft.Playwright;
using PromptBank.Models;
using PromptBank.Tests.Infrastructure;

namespace PromptBank.Tests.Tests;

/// <summary>
/// E2E tests for the semantic search feature on the Index page (<c>GET /?q=…</c>).
/// Tests use the real <see cref="SmartComponents.LocalEmbeddings.LocalEmbedder"/> via
/// <see cref="E2ETestBase.SeedWithEmbeddingsAsync"/> so that prompts have valid embeddings
/// and the full search pipeline is exercised end-to-end.
/// </summary>
[Collection("Playwright")]
public sealed class SearchTests : E2ETestBase
{
    /// <summary>
    /// Initialises the test class with the shared Playwright browser fixture.
    /// </summary>
    /// <param name="playwright">The Playwright fixture provided by the xUnit collection.</param>
    public SearchTests(PlaywrightFixture playwright) : base(playwright) { }

    // ── Prompts used across tests ─────────────────────────────────────────────

    private Prompt SqlPrompt() => new()
    {
        Id = 1,
        Title = "Generate SQL query",
        Description = "Produces a SQL query from a natural-language description and a schema.",
        Content = "Write a SQL query to {{task}}. Schema: {{schema}}",
        OwnerId = TestUserId
    };

    private Prompt UnitTestPrompt() => new()
    {
        Id = 2,
        Title = "Write unit tests",
        Description = "Generates xUnit unit tests for a given method covering happy paths and edge cases.",
        Content = "Write xUnit unit tests for the following method: {{method}}",
        OwnerId = TestUserId
    };

    // ── Tests ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that the search input and submit button are rendered on the Index page.
    /// </summary>
    [Fact]
    public async Task SearchBox_IsVisible_OnIndexPage()
    {
        await Page.GotoAsync($"{BaseUrl}/");

        await Expect(Page.Locator("input[name='q']")).ToBeVisibleAsync();
        await Expect(Page.Locator("button[type='submit']")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Verifies that submitting a semantically relevant query returns only the matching prompt
    /// and hides unrelated ones.
    /// </summary>
    [Fact]
    public async Task Search_ReturnsOnlyMatchingPrompt_WhenQueryIsRelevant()
    {
        await SeedWithEmbeddingsAsync(SqlPrompt(), UnitTestPrompt());

        await Page.GotoAsync($"{BaseUrl}/");
        await Page.Locator("input[name='q']").FillAsync("sql query");
        await Page.Locator("button[type='submit']").ClickAsync();

        await Expect(Page.GetByText("Generate SQL query")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Write unit tests")).ToBeHiddenAsync();
    }

    /// <summary>
    /// Verifies that the subtitle updates to show the result count and the search term.
    /// </summary>
    [Fact]
    public async Task Search_ShowsResultCountAndQuery_InSubtitle()
    {
        await SeedWithEmbeddingsAsync(SqlPrompt(), UnitTestPrompt());

        await Page.GotoAsync($"{BaseUrl}/?q=sql+query");

        // Subtitle should say something like "1 prompt found for "sql query""
        await Expect(Page.Locator("p.text-muted").Filter(new() { HasText = "found for" })).ToBeVisibleAsync();
    }

    /// <summary>
    /// Verifies that an unrelated query (outside the software domain) returns no results
    /// and shows the "No results found" empty state.
    /// </summary>
    [Fact]
    public async Task Search_ShowsNoResults_WhenQueryIsUnrelated()
    {
        await SeedWithEmbeddingsAsync(SqlPrompt(), UnitTestPrompt());

        await Page.GotoAsync($"{BaseUrl}/");
        await Page.Locator("input[name='q']").FillAsync("cooking recipes");
        await Page.Locator("button[type='submit']").ClickAsync();

        await Expect(Page.GetByText("No results found")).ToBeVisibleAsync();
        await Expect(Page.Locator(".prompt-card")).ToHaveCountAsync(0);
    }

    /// <summary>
    /// Verifies that clicking the clear (×) button after a search restores the full prompt list.
    /// </summary>
    [Fact]
    public async Task Search_ClearButton_RestoresFullList()
    {
        await SeedWithEmbeddingsAsync(SqlPrompt(), UnitTestPrompt());

        // Navigate with a query so the clear button appears
        await Page.GotoAsync($"{BaseUrl}/?q=sql+query");
        await Expect(Page.GetByText("Generate SQL query")).ToBeVisibleAsync();

        // Click the clear (×) button
        await Page.Locator("a[aria-label='Clear search']").ClickAsync();

        // Both prompts should be visible again
        await Expect(Page.GetByText("Generate SQL query")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Write unit tests")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Verifies that navigating to <c>/?q=</c> (empty query) shows all prompts normally.
    /// </summary>
    [Fact]
    public async Task Search_EmptyQuery_ShowsAllPrompts()
    {
        await SeedWithEmbeddingsAsync(SqlPrompt(), UnitTestPrompt());

        await Page.GotoAsync($"{BaseUrl}/?q=");

        await Expect(Page.GetByText("Generate SQL query")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Write unit tests")).ToBeVisibleAsync();
        await Expect(Page.GetByText("in the bank")).ToBeVisibleAsync();
    }
}
