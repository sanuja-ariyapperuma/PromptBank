using PromptBank.Models;
using PromptBank.Tests.Infrastructure;

namespace PromptBank.Tests.Tests;

/// <summary>
/// E2E tests for the dark / light theme acceptance criteria.
/// Covers the default dark-mode load, the toggle button switching the theme attribute,
/// localStorage persistence of the user's choice, restoration of the persisted choice
/// after a page reload, and correct copy-button behaviour after a theme switch.
/// </summary>
[Collection("Playwright")]
public sealed class DarkThemeTests : E2ETestBase
{
    /// <summary>
    /// Initialises the test class with the shared Playwright browser fixture.
    /// </summary>
    /// <param name="playwright">The Playwright fixture provided by the xUnit collection.</param>
    public DarkThemeTests(PlaywrightFixture playwright) : base(playwright) { }

    // ── AC-2 ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// AC-2: Verifies that the application loads in dark mode by default — the
    /// <c>data-bs-theme</c> attribute on the <c>&lt;html&gt;</c> element must be
    /// <c>"dark"</c> on first visit without any prior localStorage preference.
    /// </summary>
    [Fact]
    public async Task App_LoadsInDarkMode_ByDefault()
    {
        // Act – navigate to the home page in a fresh browser context (no localStorage).
        await Page.GotoAsync($"{BaseUrl}/");

        // Assert – the html element must carry data-bs-theme="dark".
        var theme = await Page.EvaluateAsync<string>(
            "() => document.documentElement.getAttribute('data-bs-theme')");

        Assert.Equal("dark", theme);
    }

    // ── AC-3a ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// AC-3a: Verifies that clicking the theme toggle button switches the
    /// <c>data-bs-theme</c> attribute on the <c>&lt;html&gt;</c> element from
    /// <c>"dark"</c> to <c>"light"</c>.
    /// </summary>
    [Fact]
    public async Task ThemeToggle_SwitchesToLightMode_WhenClicked()
    {
        // Arrange – navigate to the home page (starts in dark mode).
        await Page.GotoAsync($"{BaseUrl}/");

        // Act – click the theme toggle button in the navbar.
        await Page.ClickAsync("#themeToggle");

        // Assert – the html element must now carry data-bs-theme="light".
        var theme = await Page.EvaluateAsync<string>(
            "() => document.documentElement.getAttribute('data-bs-theme')");

        Assert.Equal("light", theme);
    }

    // ── AC-3b ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// AC-3b: Verifies that after clicking the theme toggle button the user's choice
    /// is persisted to <c>localStorage</c> under the key <c>'pb-theme'</c>.
    /// </summary>
    [Fact]
    public async Task ThemeToggle_PersistsChoice_InLocalStorage()
    {
        // Arrange – navigate to the home page (starts in dark mode).
        await Page.GotoAsync($"{BaseUrl}/");

        // Act – click the theme toggle to switch to light mode.
        await Page.ClickAsync("#themeToggle");

        // Assert – localStorage must record the selected theme.
        var storedTheme = await Page.EvaluateAsync<string>(
            "() => localStorage.getItem('pb-theme')");

        Assert.Equal("light", storedTheme);
    }

    // ── AC-3c ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// AC-3c: Verifies that after switching the theme and reloading the page the
    /// application restores the persisted theme preference (the <c>data-bs-theme</c>
    /// attribute on <c>&lt;html&gt;</c> must match what was stored).
    /// </summary>
    [Fact]
    public async Task ThemeToggle_RestoresPersistedChoice_OnReload()
    {
        // Arrange – navigate to the home page and switch to light mode.
        await Page.GotoAsync($"{BaseUrl}/");
        await Page.ClickAsync("#themeToggle");

        // Confirm the attribute changed before reloading.
        var themeBeforeReload = await Page.EvaluateAsync<string>(
            "() => document.documentElement.getAttribute('data-bs-theme')");
        Assert.Equal("light", themeBeforeReload);

        // Act – reload the page to test persistence.
        await Page.ReloadAsync();

        // Assert – the html element must still carry the previously persisted theme.
        var themeAfterReload = await Page.EvaluateAsync<string>(
            "() => document.documentElement.getAttribute('data-bs-theme')");

        Assert.Equal("light", themeAfterReload);
    }

    // ── AC-8 ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// AC-8: Verifies that the copy-to-clipboard button still works correctly after the
    /// theme has been switched, ensuring that a theme change does not break interactive
    /// JavaScript features on the page.
    /// </summary>
    [Fact]
    public async Task CopyButton_WorksAfterThemeSwitch()
    {
        // Arrange – seed a prompt with known content.
        const string expectedContent = "Copy me after theme switch.";
        await SeedAsync(new Prompt
        {
            Id = 1, Title = "Theme Copy Test", Content = expectedContent,
            Description = "A description for the copy test.", OwnerId = TestUserId
        });

        // Navigate to the home page.
        await Page.GotoAsync($"{BaseUrl}/");

        // Act – toggle the theme first, then click the Copy button.
        await Page.ClickAsync("#themeToggle");
        await Page.Locator(".btn-copy").First.ClickAsync();

        // Assert – the clipboard must contain the prompt's content text.
        var clipboardText = await Page.EvaluateAsync<string>(
            "() => navigator.clipboard.readText()");

        Assert.Equal(expectedContent, clipboardText);
    }
}
