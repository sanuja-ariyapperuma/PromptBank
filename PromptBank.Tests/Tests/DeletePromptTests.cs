using PromptBank.Models;
using PromptBank.Tests.Infrastructure;

namespace PromptBank.Tests.Tests;

/// <summary>
/// E2E tests for the Delete Prompt confirmation flow
/// (<c>GET /Prompts/Delete?id=N</c>, <c>POST /Prompts/Delete</c>).
/// Covers navigation, confirmation-page content, cancel behaviour, and confirmed deletion.
/// </summary>
[Collection("Playwright")]
public sealed class DeletePromptTests : E2ETestBase
{
    /// <summary>
    /// Initialises the test class with the shared Playwright browser fixture.
    /// </summary>
    /// <param name="playwright">The Playwright fixture provided by the xUnit collection.</param>
    public DeletePromptTests(PlaywrightFixture playwright) : base(playwright) { }

    /// <summary>
    /// Verifies that clicking the Delete link on a card navigates to the
    /// Delete confirmation page.
    /// </summary>
    [Fact]
    public async Task DeleteLink_NavigatesToConfirmationPage_WhenClicked()
    {
        // Arrange – Delete requires authentication and ownership.
        await SeedAsync(new Prompt { Id = 1, Title = "Delete Nav Test", Content = "Content", Description = "A test description.", OwnerId = TestUserId });
        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");

        // Act – click the Delete link (btn-outline-danger) on the first card.
        await Page.Locator("a.btn-outline-danger").First.ClickAsync();

        // Assert
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/Prompts/Delete"));
    }

    /// <summary>
    /// Verifies that the Delete confirmation page displays the title of the prompt
    /// being deleted, so the user knows exactly what they are about to remove.
    /// </summary>
    [Fact]
    public async Task ConfirmationPage_ShowsPromptTitle_WhenLoaded()
    {
        // Arrange
        await SeedAsync(new Prompt { Id = 1, Title = "To Delete", Content = "Content", Description = "A test description.", OwnerId = TestUserId });
        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");
        await Page.Locator("a.btn-outline-danger").First.ClickAsync();

        // Assert – the confirmation card must show the prompt title.
        await Expect(Page.GetByText("To Delete")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Verifies that clicking Cancel on the Delete confirmation page navigates back
    /// to the Index page.
    /// </summary>
    [Fact]
    public async Task CancelDelete_ReturnsToIndex_WhenCancelClicked()
    {
        // Arrange
        await SeedAsync(new Prompt { Id = 1, Title = "Cancel Delete Test", Content = "Content", Description = "A test description.", OwnerId = TestUserId });
        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");
        await Page.Locator("a.btn-outline-danger").First.ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/Prompts/Delete"));

        // Act – click the Cancel link.
        await Page.GetByRole(Microsoft.Playwright.AriaRole.Link, new() { Name = "Cancel" }).ClickAsync();

        // Assert – should be back on the Index page.
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/$"));
    }

    /// <summary>
    /// Verifies that cancelling a delete does not remove the prompt from the listing.
    /// </summary>
    [Fact]
    public async Task CancelDelete_PromptStillInList_AfterCancelling()
    {
        // Arrange
        await SeedAsync(new Prompt { Id = 1, Title = "Still Here", Content = "Content", Description = "A test description.", OwnerId = TestUserId });
        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");
        await Page.Locator("a.btn-outline-danger").First.ClickAsync();

        // Act – cancel the deletion.
        await Page.GetByRole(Microsoft.Playwright.AriaRole.Link, new() { Name = "Cancel" }).ClickAsync();

        // Assert – the prompt must still be visible in the listing.
        await Expect(Page.GetByText("Still Here")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Verifies that confirming a deletion redirects the user back to the Index page.
    /// </summary>
    [Fact]
    public async Task ConfirmDelete_RedirectsToIndex_AfterDeletion()
    {
        // Arrange
        await SeedAsync(new Prompt { Id = 1, Title = "Confirm Delete Test", Content = "Content", Description = "A test description.", OwnerId = TestUserId });
        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");
        await Page.Locator("a.btn-outline-danger").First.ClickAsync();

        // Act – click the "Yes, Delete It" confirm button.
        await Page.GetByRole(Microsoft.Playwright.AriaRole.Button, new() { Name = "Yes, Delete It" }).ClickAsync();

        // Assert – must redirect to Index.
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/$"));
    }

    /// <summary>
    /// Verifies that confirming deletion removes the prompt from the Index listing.
    /// </summary>
    [Fact]
    public async Task ConfirmDelete_PromptRemovedFromList_AfterDeletion()
    {
        // Arrange
        await SeedAsync(new Prompt { Id = 1, Title = "Gone After Delete", Content = "Content", Description = "A test description.", OwnerId = TestUserId });
        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");
        await Page.Locator("a.btn-outline-danger").First.ClickAsync();

        // Act
        await Page.GetByRole(Microsoft.Playwright.AriaRole.Button, new() { Name = "Yes, Delete It" }).ClickAsync();

        // Assert – the deleted prompt's title must no longer appear on the page.
        await Expect(Page.GetByText("Gone After Delete")).ToBeHiddenAsync();
    }
}
