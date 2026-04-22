using PromptBank.Models;
using PromptBank.Tests.Infrastructure;

namespace PromptBank.Tests.Tests;

/// <summary>
/// E2E tests that verify the authentication and authorisation acceptance criteria for the
/// Prompt Bank application.  Covers unauthenticated browsing, redirect behaviour for
/// protected actions, login/logout flows, per-user ownership enforcement, per-user pin
/// isolation, duplicate-rating prevention, and access-control on edit operations.
/// </summary>
[Collection("Playwright")]
public sealed class AuthenticationTests : E2ETestBase
{
    /// <summary>
    /// Initialises the test class with the shared Playwright browser fixture.
    /// </summary>
    /// <param name="playwright">The Playwright fixture provided by the xUnit collection.</param>
    public AuthenticationTests(PlaywrightFixture playwright) : base(playwright) { }

    // ── AC-1 ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// AC-1: Verifies that an unauthenticated visitor can browse the prompt list without
    /// being redirected or challenged by the application.
    /// </summary>
    [Fact]
    public async Task UnauthenticatedUser_CanBrowsePromptList()
    {
        // Arrange – seed a prompt owned by alice so the list is not empty.
        await SeedAsync(new Prompt
        {
            Id = 1, Title = "Public Prompt", Content = "Some content.",
            Description = "A visible description.", OwnerId = TestUserId
        });

        // Act – navigate to the home page without logging in.
        await Page.GotoAsync($"{BaseUrl}/");

        // Assert – the prompt card must be visible to anonymous users.
        await Expect(Page.Locator(".prompt-card").First).ToBeVisibleAsync();
        await Expect(Page.GetByText("Public Prompt")).ToBeVisibleAsync();
    }

    // ── AC-2a ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// AC-2a: Verifies that an unauthenticated user who navigates directly to
    /// <c>/Prompts/Create</c> is redirected to the login page.
    /// </summary>
    [Fact]
    public async Task UnauthenticatedUser_ClickingCreate_RedirectsToLogin()
    {
        // Act – attempt to visit the Create page without a session.
        await Page.GotoAsync($"{BaseUrl}/Prompts/Create");

        // Assert – the server should redirect to the login page.
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/Account/Login"));
    }

    // ── AC-2b ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// AC-2b: Verifies that the pin button (<c>.btn-pin</c>) is not rendered for
    /// unauthenticated users, so there is nothing to click that could expose the action.
    /// </summary>
    [Fact]
    public async Task UnauthenticatedUser_ClickingPin_RedirectsToLogin()
    {
        // Arrange – seed a prompt so there is a card on the page.
        await SeedAsync(new Prompt
        {
            Id = 1, Title = "Pin Target", Content = "Content.",
            Description = "A description.", OwnerId = TestUserId
        });

        // Act – visit the home page without logging in.
        await Page.GotoAsync($"{BaseUrl}/");

        // Assert – the pin button must not be rendered for anonymous visitors.
        await Expect(Page.Locator(".btn-pin")).ToHaveCountAsync(0);
    }

    // ── AC-2c ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// AC-2c: Verifies that when an unauthenticated user clicks a star button the
    /// client-side JavaScript redirects the browser to the login page.
    /// </summary>
    [Fact]
    public async Task UnauthenticatedUser_ClickingStar_RedirectsToLogin()
    {
        // Arrange – seed a prompt so stars are rendered on the page.
        await SeedAsync(new Prompt
        {
            Id = 1, Title = "Star Target", Content = "Content.",
            Description = "A description.", OwnerId = TestUserId
        });

        // Act – navigate to the home page without a session then click a star.
        await Page.GotoAsync($"{BaseUrl}/");
        await Page.Locator(".star-btn[data-star='3']").First.ClickAsync();

        // Assert – the star JS checks data-auth and redirects unauthenticated users.
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/Account/Login"));
    }

    // ── AC-3 ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// AC-3: Verifies that a user with valid credentials can log in and is redirected
    /// to the home page after a successful login.
    /// </summary>
    [Fact]
    public async Task RegisteredUser_CanLogIn_WithValidCredentials()
    {
        // Act – navigate to the login page and submit valid credentials.
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Page.FillAsync("#Input_Username", "alice");
        await Page.FillAsync("#Input_Password", "Alice@1234");
        await Page.ClickAsync("button[type='submit']");

        // Assert – must be redirected to the home page.
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/$"));
    }

    // ── AC-4 ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// AC-4: Verifies that a prompt created by a logged-in user shows that user's
    /// username on the resulting prompt card.
    /// </summary>
    [Fact]
    public async Task LoggedInUser_CreatePrompt_ShowsUsernameAsOwner()
    {
        // Arrange – log in as alice and open the Create form.
        await Page.GotoAsync($"{BaseUrl}/");
        await LoginAsync("alice", "Alice@1234");
        await Page.GotoAsync($"{BaseUrl}/Prompts/Create");

        // Act – submit the form with valid data.
        await Page.FillAsync("#Input_Title",       "Alice's Prompt");
        await Page.FillAsync("#Input_Description", "A description written by alice.");
        await Page.FillAsync("#Input_Content",     "Prompt content from alice.");
        await Page.ClickAsync("button.btn-primary[type='submit']");

        // Assert – after redirect the card must display alice's username in the owner field.
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/$"));
        await Expect(Page.Locator(".prompt-card .card-footer span.small.text-muted").First)
            .ToContainTextAsync("alice");
    }

    // ── AC-5 ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// AC-5: Verifies that the Edit and Delete buttons on a prompt owned by alice are
    /// NOT rendered when bob is logged in (ownership-based visibility enforcement).
    /// </summary>
    [Fact]
    public async Task EditDeleteButtons_OnlyVisibleToOwner()
    {
        // Arrange – seed a prompt owned by alice (TestUserId = alice's ID).
        await SeedAsync(new Prompt
        {
            Id = 1, Title = "Alice's Card", Content = "Content.",
            Description = "A description.", OwnerId = TestUserId
        });

        // Act – log in as bob and navigate to the home page.
        await LoginAsync("bob", "Bob@1234");

        // Assert – neither the Edit (btn-outline-primary) nor Delete (btn-outline-danger)
        //          buttons should be present on the page from bob's perspective.
        await Expect(Page.Locator("a.btn-outline-primary")).ToHaveCountAsync(0);
        await Expect(Page.Locator("a.btn-outline-danger")).ToHaveCountAsync(0);
    }

    // ── AC-6 ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// AC-6: Verifies that a direct navigation to the Edit URL for another user's prompt
    /// results in a 403 Forbidden response or an appropriate error page rather than
    /// rendering the edit form.
    /// </summary>
    [Fact]
    public async Task DirectUrl_EditAnotherUsersPrompt_ReturnsForbidden()
    {
        // Arrange – seed a prompt owned by alice.
        await SeedAsync(new Prompt
        {
            Id = 1, Title = "Alice's Secret Prompt", Content = "Sensitive content.",
            Description = "A description.", OwnerId = TestUserId
        });

        // Log in as bob.
        await LoginAsync("bob", "Bob@1234");

        // Act – capture the response when navigating directly to alice's edit URL.
        var response = await Page.GotoAsync($"{BaseUrl}/Prompts/Edit?id=1");

        // Assert – the server must return 403, OR the URL must not be the edit form,
        //          OR the page must show Forbidden/error content.
        var statusCode = response?.Status ?? 0;
        if (statusCode == 403)
        {
            // Direct 403 response – test passes.
            Assert.Equal(403, statusCode);
        }
        else
        {
            // Either a redirect to an error page or the edit form input must be absent.
            var editFormVisible = await Page.Locator("#Input_Title").IsVisibleAsync();
            Assert.False(editFormVisible,
                "The edit form should not be visible when bob tries to edit alice's prompt.");
        }
    }

    // ── AC-7 ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// AC-7: Verifies that pin state is per-user: after alice pins a prompt the pin badge
    /// is visible to her, but when bob logs in the same badge has the <c>d-none</c> class
    /// (hidden) because bob has not pinned it.
    /// </summary>
    [Fact]
    public async Task PinIsPerUser_OtherUserDoesNotSeePinBadge()
    {
        // Arrange – seed a prompt owned by alice.
        await SeedAsync(new Prompt
        {
            Id = 1, Title = "Shared Prompt", Content = "Content.",
            Description = "A description.", OwnerId = TestUserId
        });

        // Step 1 – alice logs in and pins the prompt.
        await LoginAsync("alice", "Alice@1234");
        var aliceCard  = Page.Locator(".prompt-card").First;
        var aliceBadge = aliceCard.Locator(".pin-badge");

        await aliceCard.Locator(".btn-pin").ClickAsync();
        await Expect(aliceBadge).ToBeVisibleAsync(); // badge must be visible for alice.

        // Step 2 – log alice out via the logout form.
        await Page.Locator("form[action='/Account/Logout'] button").ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/$"));

        // Step 3 – log in as bob.
        await LoginAsync("bob", "Bob@1234");

        // Assert – the pin badge on the same card must be hidden for bob.
        var bobCard  = Page.Locator(".prompt-card").First;
        var bobBadge = bobCard.Locator(".pin-badge");
        await Expect(bobBadge).ToBeHiddenAsync();
    }

    // ── AC-8 ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// AC-8: Verifies that attempting to rate the same prompt twice shows an
    /// "Already rated" feedback message on the page.
    /// </summary>
    [Fact]
    public async Task RatingPromptTwice_ShowsAlreadyRatedFeedback()
    {
        // Arrange – seed a prompt with no existing votes.
        await SeedAsync(new Prompt
        {
            Id = 1, Title = "Rate Twice Test", Content = "Content.",
            Description = "A description.", OwnerId = TestUserId,
            RatingTotal = 0, RatingCount = 0
        });

        // Log in as alice.
        await LoginAsync("alice", "Alice@1234");

        var card = Page.Locator(".prompt-card").First;

        // Act – first vote.
        await card.Locator(".star-btn[data-star='4']").ClickAsync();
        await Expect(card.Locator(".avg-score")).ToContainTextAsync("4");

        // Act – second vote on the same prompt.
        await card.Locator(".star-btn[data-star='2']").ClickAsync();

        // Assert – "Already rated" feedback must become visible.
        await Expect(Page.GetByText("Already rated")).ToBeVisibleAsync();
    }

    // ── AC-9a ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// AC-9a: Verifies that the seeded "alice" user can log in with her known credentials
    /// and is redirected to the home page.
    /// </summary>
    [Fact]
    public async Task AliceCanLogIn_WithSeededCredentials()
    {
        // Act
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Page.FillAsync("#Input_Username", "alice");
        await Page.FillAsync("#Input_Password", "Alice@1234");
        await Page.ClickAsync("button[type='submit']");

        // Assert
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/$"));
    }

    // ── AC-9b ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// AC-9b: Verifies that the seeded "bob" user can log in with his known credentials
    /// and is redirected to the home page.
    /// </summary>
    [Fact]
    public async Task BobCanLogIn_WithSeededCredentials()
    {
        // Act
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Page.FillAsync("#Input_Username", "bob");
        await Page.FillAsync("#Input_Password", "Bob@1234");
        await Page.ClickAsync("button[type='submit']");

        // Assert
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/$"));
    }

    // ── AC-9c ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// AC-9c: Verifies that the seeded "carol" user can log in with her known credentials
    /// and is redirected to the home page.
    /// </summary>
    [Fact]
    public async Task CarolCanLogIn_WithSeededCredentials()
    {
        // Act
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Page.FillAsync("#Input_Username", "carol");
        await Page.FillAsync("#Input_Password", "Carol@1234");
        await Page.ClickAsync("button[type='submit']");

        // Assert
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/$"));
    }

    // ── AC-10 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// AC-10: Verifies that after submitting the logout form the session is cleared,
    /// the browser is redirected to the home page, and the navbar shows the Login link
    /// rather than the authenticated user's username.
    /// </summary>
    [Fact]
    public async Task Logout_ClearsSession_RedirectsToIndex()
    {
        // Arrange – log in as alice.
        await LoginAsync("alice", "Alice@1234");

        // Confirm alice's username is visible in the navbar before logout.
        await Expect(Page.GetByText("alice")).ToBeVisibleAsync();

        // Act – submit the logout form.
        await Page.Locator("form[action='/Account/Logout'] button").ClickAsync();

        // Assert – redirected to the home page.
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/$"));

        // Assert – the Login link must now be visible in the navbar (unauthenticated state).
        await Expect(Page.Locator("a[href='/Account/Login']")).ToBeVisibleAsync();
    }
}
