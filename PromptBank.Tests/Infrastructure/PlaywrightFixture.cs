using Microsoft.Playwright;

namespace PromptBank.Tests.Infrastructure;

/// <summary>
/// An xUnit class-fixture that owns a single Playwright <see cref="IBrowser"/> instance for the
/// lifetime of one test class.
/// <para>
/// All tests that inherit from <see cref="E2ETestBase"/> receive this fixture via constructor
/// injection and share the same browser process, while each individual test gets its own
/// <see cref="IBrowserContext"/> (created in <see cref="E2ETestBase.InitializeAsync"/>).
/// </para>
/// </summary>
/// <remarks>
/// Declared as an xUnit collection fixture via <see cref="PlaywrightCollection"/> so that a
/// single Chromium process is reused across every test class in the "Playwright" collection.
/// </remarks>
public sealed class PlaywrightFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;

    /// <summary>
    /// Gets the Chromium browser instance shared across all tests in the collection.
    /// </summary>
    public IBrowser Browser { get; private set; } = null!;

    // ── IAsyncLifetime ────────────────────────────────────────────────────────

    /// <summary>
    /// Installs the Chromium browser binaries (if not already present) and launches a
    /// headless Chromium browser instance.
    /// </summary>
    /// <returns>A task that completes once the browser is ready to accept new contexts.</returns>
    public async Task InitializeAsync()
    {
        // Ensure the Chromium binaries are available on the current machine.
        // This is a no-op when the binaries are already installed.
        Microsoft.Playwright.Program.Main(["install", "chromium"]);

        _playwright = await Playwright.CreateAsync();

        var isCI = Environment.GetEnvironmentVariable("CI") == "true";

        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = isCI,
            SlowMo   = isCI ? 0 : 100   // ms delay between actions — disabled in CI for speed
        });
    }

    /// <summary>
    /// Closes the browser and releases the Playwright instance.
    /// </summary>
    /// <returns>A task that completes once all resources are freed.</returns>
    public async Task DisposeAsync()
    {
        if (Browser is not null)
            await Browser.CloseAsync();

        _playwright?.Dispose();
    }
}

/// <summary>
/// Defines the "Playwright" xUnit test collection.
/// All test classes decorated with <c>[Collection("Playwright")]</c> share a single
/// <see cref="PlaywrightFixture"/> instance, which means one Chromium process for all E2E tests.
/// </summary>
[CollectionDefinition("Playwright")]
public sealed class PlaywrightCollection : ICollectionFixture<PlaywrightFixture> { }
