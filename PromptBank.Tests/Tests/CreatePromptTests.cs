using PromptBank.Models;
using PromptBank.Tests.Infrastructure;

namespace PromptBank.Tests.Tests;

/// <summary>
/// E2E tests for the Create Prompt form (<c>GET /Prompts/Create</c>, <c>POST /Prompts/Create</c>).
/// Covers navigation, form field presence, successful submission, redirect behaviour,
/// and server/client-side validation errors for each required field.
/// </summary>
[Collection("Playwright")]
public sealed class CreatePromptTests : E2ETestBase
{
    /// <summary>
    /// Initialises the test class with the shared Playwright browser fixture.
    /// </summary>
    /// <param name="playwright">The Playwright fixture provided by the xUnit collection.</param>
    public CreatePromptTests(PlaywrightFixture playwright) : base(playwright) { }

    /// <summary>
    /// Verifies that after logging in, clicking the "Add Prompt" button on the Index page
    /// navigates to the Create page.
    /// </summary>
    [Fact]
    public async Task NavigateToCreate_FromNavButton_LandsOnCreatePage()
    {
        // Arrange – log in and navigate to Index first.
        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");

        // Act – click the "Add Prompt" link rendered on the Index page for authenticated users.
        await Page.Locator("a[href='/Prompts/Create']").First.ClickAsync();

        // Assert
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/Prompts/Create"));
    }

    /// <summary>
    /// Verifies that the Create page renders the Title, Description, and Content inputs.
    /// </summary>
    [Fact]
    public async Task CreateForm_HasRequiredFields_OnPageLoad()
    {
        // Arrange – Create requires authentication.
        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");

        // Act
        await Page.GotoAsync($"{BaseUrl}/Prompts/Create");

        // Assert – all three required inputs must be present and visible.
        await Expect(Page.Locator("#Input_Title")).ToBeVisibleAsync();
        await Expect(Page.Locator("#Input_Description")).ToBeVisibleAsync();
        await Expect(Page.Locator("#Input_Content")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Verifies that submitting the Create form with valid data redirects the user
    /// back to the Index page (<c>/</c>).
    /// </summary>
    [Fact]
    public async Task CreatePrompt_ValidData_RedirectsToIndex()
    {
        // Arrange – Create requires authentication.
        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");
        await Page.GotoAsync($"{BaseUrl}/Prompts/Create");

        // Act – fill all required fields and submit.
        await Page.FillAsync("#Input_Title",       "Valid Title");
        await Page.FillAsync("#Input_Description", "A valid description for testing.");
        await Page.FillAsync("#Input_Content",     "Valid prompt content here.");
        await Page.ClickAsync("button.btn-primary[type='submit']");

        // Assert – should redirect to the listing page.
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/$"));
    }

    /// <summary>
    /// Verifies that a newly created prompt appears in the Index listing immediately
    /// after the form is submitted.
    /// </summary>
    [Fact]
    public async Task CreatePrompt_AppearsInList_AfterSuccessfulSubmission()
    {
        // Arrange – Create requires authentication.
        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");
        await Page.GotoAsync($"{BaseUrl}/Prompts/Create");

        // Act
        await Page.FillAsync("#Input_Title",       "My Test Prompt");
        await Page.FillAsync("#Input_Description", "A description of the test prompt.");
        await Page.FillAsync("#Input_Content",     "Test content.");
        await Page.ClickAsync("button.btn-primary[type='submit']");

        // Assert – the title must be visible on the Index page after redirect.
        await Expect(Page.GetByText("My Test Prompt")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Verifies that submitting the form without a Description shows a validation error
    /// for that specific field.
    /// </summary>
    [Fact]
    public async Task CreatePrompt_EmptyDescription_ShowsValidationError()
    {
        // Arrange – Create requires authentication.
        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");
        await Page.GotoAsync($"{BaseUrl}/Prompts/Create");

        // Act – fill Title and Content but leave Description blank.
        await Page.FillAsync("#Input_Title",   "Some Title");
        await Page.FillAsync("#Input_Content", "Some content.");
        await Page.ClickAsync("button.btn-primary[type='submit']");

        // Assert – validation span for Description must be visible and non-empty.
        var errorSpan = Page.Locator("[data-valmsg-for='Input.Description']");
        await Expect(errorSpan).ToBeVisibleAsync();
        await Expect(errorSpan).Not.ToBeEmptyAsync();
    }

    /// <summary>
    /// Verifies that submitting the form without a Title shows a validation error
    /// for that specific field.
    /// </summary>
    [Fact]
    public async Task CreatePrompt_EmptyTitle_ShowsValidationError()
    {
        // Arrange – Create requires authentication.
        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");
        await Page.GotoAsync($"{BaseUrl}/Prompts/Create");

        // Act – fill Description and Content but leave Title blank.
        await Page.FillAsync("#Input_Description", "Some description.");
        await Page.FillAsync("#Input_Content",     "Some content.");
        await Page.ClickAsync("button.btn-primary[type='submit']");

        // Assert
        var errorSpan = Page.Locator("[data-valmsg-for='Input.Title']");
        await Expect(errorSpan).ToBeVisibleAsync();
        await Expect(errorSpan).Not.ToBeEmptyAsync();
    }

    /// <summary>
    /// Verifies that submitting the form without prompt Content shows a validation error
    /// for that specific field.
    /// </summary>
    [Fact]
    public async Task CreatePrompt_EmptyContent_ShowsValidationError()
    {
        // Arrange – Create requires authentication.
        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");
        await Page.GotoAsync($"{BaseUrl}/Prompts/Create");

        // Act – fill Title and Description but leave Content blank.
        await Page.FillAsync("#Input_Title",       "Some Title");
        await Page.FillAsync("#Input_Description", "Some description.");
        await Page.ClickAsync("button.btn-primary[type='submit']");

        // Assert
        var errorSpan = Page.Locator("[data-valmsg-for='Input.Content']");
        await Expect(errorSpan).ToBeVisibleAsync();
        await Expect(errorSpan).Not.ToBeEmptyAsync();
    }
}
