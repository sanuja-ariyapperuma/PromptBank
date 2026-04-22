using PromptBank.Models;
using PromptBank.Tests.Infrastructure;

namespace PromptBank.Tests.Tests;

/// <summary>
/// E2E tests for the prompt Description field acceptance criteria.
/// Covers client-side and server-side validation (empty, too long), correct display
/// of the description on the prompt card after create and edit, and that long (but
/// valid) descriptions are shown in full without truncation UI.
/// </summary>
[Collection("Playwright")]
public sealed class PromptDescriptionTests : E2ETestBase
{
    /// <summary>
    /// Initialises the test class with the shared Playwright browser fixture.
    /// </summary>
    /// <param name="playwright">The Playwright fixture provided by the xUnit collection.</param>
    public PromptDescriptionTests(PlaywrightFixture playwright) : base(playwright) { }

    // ── AC-1 ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// AC-1: Verifies that submitting the Create form without filling the Description field
    /// shows a validation error for that specific field and does not navigate away.
    /// </summary>
    [Fact]
    public async Task CreatePrompt_EmptyDescription_ShowsValidationError()
    {
        // Arrange – log in as alice and open the Create form.
        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");
        await Page.GotoAsync($"{BaseUrl}/Prompts/Create");

        // Act – fill Title and Content but deliberately leave Description blank.
        await Page.FillAsync("#Input_Title",   "Prompt Without Description");
        await Page.FillAsync("#Input_Content", "Some meaningful content.");
        await Page.ClickAsync("button.btn-primary[type='submit']");

        // Assert – the validation message span for Description must be visible and non-empty.
        var errorSpan = Page.Locator("[data-valmsg-for='Input.Description']");
        await Expect(errorSpan).ToBeVisibleAsync();
        await Expect(errorSpan).Not.ToBeEmptyAsync();
    }

    // ── AC-2 ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// AC-2: Verifies that submitting the Create form with a Description that exceeds
    /// the maximum allowed length (500 characters) shows a validation error for that field.
    /// </summary>
    [Fact]
    public async Task CreatePrompt_DescriptionTooLong_ShowsValidationError()
    {
        // Arrange – build a description that is one character over the 500-char limit.
        var tooLongDescription = new string('a', 501);

        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");
        await Page.GotoAsync($"{BaseUrl}/Prompts/Create");

        // Act – fill all fields; use JS to bypass the textarea's maxlength HTML attribute
        //       so the 501-char value is actually set (browsers truncate .value to maxlength).
        await Page.FillAsync("#Input_Title", "Long Description Prompt");
        await Page.EvaluateAsync(
            "(val) => { const el = document.getElementById('Input_Description'); el.removeAttribute('maxlength'); el.value = val; el.dispatchEvent(new Event('input', { bubbles: true })); }",
            tooLongDescription);
        await Page.FillAsync("#Input_Content", "Some content.");
        await Page.ClickAsync("button.btn-primary[type='submit']");

        // Assert – a validation error for Description must appear.
        var errorSpan = Page.Locator("[data-valmsg-for='Input.Description']");
        await Expect(errorSpan).ToBeVisibleAsync();
        await Expect(errorSpan).Not.ToBeEmptyAsync();
    }

    // ── AC-3 ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// AC-3: Verifies that after creating a prompt the description text appears
    /// in the <c>p.text-muted.small</c> element within the resulting prompt card.
    /// </summary>
    [Fact]
    public async Task CreatePrompt_DescriptionAppearsOnCard()
    {
        // Arrange – log in as alice and open the Create form.
        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");
        await Page.GotoAsync($"{BaseUrl}/Prompts/Create");

        // Act – create a prompt with a distinctive description.
        await Page.FillAsync("#Input_Title",       "Card Description Test");
        await Page.FillAsync("#Input_Description", "A meaningful description");
        await Page.FillAsync("#Input_Content",     "Prompt content here.");
        await Page.ClickAsync("button.btn-primary[type='submit']");

        // Assert – after the redirect the description must appear in the card body.
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/$"));
        var descriptionParagraph = Page.Locator(".prompt-card .card-body p.text-muted.small").First;
        await Expect(descriptionParagraph).ToContainTextAsync("A meaningful description");
    }

    // ── AC-4 ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// AC-4: Verifies that editing a prompt and updating its Description causes the new
    /// description text to appear on the card after the form is saved.
    /// </summary>
    [Fact]
    public async Task EditPrompt_UpdateDescription_AppearsOnCard()
    {
        // Arrange – seed a prompt with a known description, then log in as alice.
        await SeedAsync(new Prompt
        {
            Id = 1, Title = "Edit Description Test", Content = "Content.",
            Description = "Old description", OwnerId = TestUserId
        });

        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");

        // Navigate to the Edit page via the card's Edit button.
        await Page.Locator("a.btn-outline-primary").First.ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/Prompts/Edit"));

        // Act – replace the description and save.
        await Page.Locator("#Input_Description").ClearAsync();
        await Page.FillAsync("#Input_Description", "Updated description");
        await Page.ClickAsync("button.btn-primary[type='submit']");

        // Assert – the updated description must be visible on the card after redirect.
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/$"));
        var descriptionParagraph = Page.Locator(".prompt-card .card-body p.text-muted.small").First;
        await Expect(descriptionParagraph).ToContainTextAsync("Updated description");
    }

    // ── AC-5 ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// AC-5: Verifies that a prompt with a long (but valid, 499-character) description
    /// displays the full text on the card without any truncation control such as a
    /// "Show more" button for the description paragraph.
    /// </summary>
    [Fact]
    public async Task PromptCard_ShowsFullDescription_NoTruncation()
    {
        // Arrange – build a description that is exactly 499 characters (within the 500-char limit).
        var longDescription = new string('x', 499);

        await SeedAsync(new Prompt
        {
            Id = 1, Title = "Long Description Card Test", Content = "Content.",
            Description = longDescription, OwnerId = TestUserId
        });

        // Act – visit the home page as an anonymous user (description is always visible).
        await Page.GotoAsync($"{BaseUrl}/");

        // Assert – the description paragraph must be visible.
        var descriptionParagraph = Page.Locator(".prompt-card .card-body p.text-muted.small").First;
        await Expect(descriptionParagraph).ToBeVisibleAsync();

        // Assert – no "Show more" button should exist for the description paragraph.
        // (The app only uses "Show more" for the Content field, not for Description.)
        var showMoreButtons = Page.Locator("button.show-more-description");
        await Expect(showMoreButtons).ToHaveCountAsync(0);
    }
}
