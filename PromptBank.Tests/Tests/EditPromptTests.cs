using PromptBank.Models;
using PromptBank.Tests.Infrastructure;

namespace PromptBank.Tests.Tests;

/// <summary>
/// E2E tests for the Edit Prompt form (<c>GET /Prompts/Edit?id=N</c>, <c>POST /Prompts/Edit</c>).
/// Covers navigation via the card's Edit link, pre-population of existing values,
/// successful save with redirect, updated title visibility, and validation errors.
/// </summary>
[Collection("Playwright")]
public sealed class EditPromptTests : E2ETestBase
{
    /// <summary>
    /// Initialises the test class with the shared Playwright browser fixture.
    /// </summary>
    /// <param name="playwright">The Playwright fixture provided by the xUnit collection.</param>
    public EditPromptTests(PlaywrightFixture playwright) : base(playwright) { }

    /// <summary>
    /// Verifies that clicking the Edit link on a prompt card navigates to the Edit page.
    /// </summary>
    [Fact]
    public async Task EditLink_NavigatesToEditPage_WhenClicked()
    {
        // Arrange – Edit requires authentication and ownership.
        await SeedAsync(new Prompt { Id = 1, Title = "Edit Nav Test", Content = "Content", Description = "A test description.", OwnerId = TestUserId });
        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");

        // Act – click the Edit link (btn-outline-primary) on the first card.
        await Page.Locator("a.btn-outline-primary").First.ClickAsync();

        // Assert
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/Prompts/Edit"));
    }

    /// <summary>
    /// Verifies that the Edit form is pre-populated with the prompt's existing values.
    /// </summary>
    [Fact]
    public async Task EditForm_PreFilled_WithExistingValues()
    {
        // Arrange – seed a prompt with a known title.
        await SeedAsync(new Prompt { Id = 1, Title = "Original Title", Content = "Original content.", Description = "Original description.", OwnerId = TestUserId });
        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");

        // Navigate to the Edit page via the card's Edit link.
        await Page.Locator("a.btn-outline-primary").First.ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/Prompts/Edit"));

        // Assert – the title input must contain the existing value.
        await Expect(Page.Locator("#Input_Title")).ToHaveValueAsync("Original Title");
    }

    /// <summary>
    /// Verifies that saving a valid edit redirects back to the Index page.
    /// </summary>
    [Fact]
    public async Task EditPrompt_ValidData_RedirectsToIndex()
    {
        // Arrange
        await SeedAsync(new Prompt { Id = 1, Title = "Edit Redirect Test", Content = "Content", Description = "A test description.", OwnerId = TestUserId });
        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");
        await Page.Locator("a.btn-outline-primary").First.ClickAsync();

        // Act – update the title and submit (Description stays pre-populated from seed).
        await Page.FillAsync("#Input_Title",   "Edited Title");
        await Page.FillAsync("#Input_Content", "Updated content.");
        await Page.ClickAsync("button.btn-primary[type='submit']");

        // Assert – must redirect to Index.
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/$"));
    }

    /// <summary>
    /// Verifies that the updated title appears in the Index listing after a successful edit.
    /// </summary>
    [Fact]
    public async Task EditPrompt_UpdatedTitle_AppearsInList()
    {
        // Arrange
        await SeedAsync(new Prompt { Id = 1, Title = "Old Title", Content = "Content", Description = "A test description.", OwnerId = TestUserId });
        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");
        await Page.Locator("a.btn-outline-primary").First.ClickAsync();

        // Act – change the title (Description stays pre-populated from seed).
        await Page.Locator("#Input_Title").ClearAsync();
        await Page.FillAsync("#Input_Title", "Updated Title");
        await Page.ClickAsync("button.btn-primary[type='submit']");

        // Assert – "Updated Title" must be visible on the Index listing.
        await Expect(Page.GetByText("Updated Title")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Verifies that clearing the Title field and submitting the Edit form shows
    /// a validation error for the Title field.
    /// </summary>
    [Fact]
    public async Task EditPrompt_EmptyTitle_ShowsValidationError()
    {
        // Arrange
        await SeedAsync(new Prompt { Id = 1, Title = "Validation Test", Content = "Content", Description = "A test description.", OwnerId = TestUserId });
        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");
        await Page.Locator("a.btn-outline-primary").First.ClickAsync();

        // Act – clear the Title and submit.
        await Page.Locator("#Input_Title").ClearAsync();
        await Page.ClickAsync("button.btn-primary[type='submit']");

        // Assert – validation error must appear for the Title field.
        var errorSpan = Page.Locator("[data-valmsg-for='Input.Title']");
        await Expect(errorSpan).ToBeVisibleAsync();
        await Expect(errorSpan).Not.ToBeEmptyAsync();
    }
}
