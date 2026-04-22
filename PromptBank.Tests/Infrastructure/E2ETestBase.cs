using Microsoft.Playwright;
using PromptBank.Models;

namespace PromptBank.Tests.Infrastructure;

/// <summary>
/// Abstract base class for all Playwright E2E tests against the Prompt Bank application.
/// <para>
/// Each concrete test class inherits this base and receives a <see cref="PlaywrightFixture"/>
/// via xUnit constructor injection (the fixture is registered through the "Playwright" collection).
/// </para>
/// <para>
/// Per-test lifecycle (driven by <see cref="IAsyncLifetime"/>):
/// <list type="bullet">
///   <item><see cref="InitializeAsync"/> — starts the Kestrel test server, creates an isolated
///         <see cref="IBrowserContext"/> with clipboard permissions, and opens a fresh
///         <see cref="IPage"/>.</item>
///   <item><see cref="DisposeAsync"/> — disposes the browser context and the web factory
///         (which also stops the Kestrel server).</item>
/// </list>
/// </para>
/// </summary>
public abstract class E2ETestBase : IAsyncLifetime
{
    private readonly PlaywrightFixture _playwright;
    private IBrowserContext _context = null!;

    // ── Protected properties ──────────────────────────────────────────────────

    /// <summary>
    /// Gets the web application factory that controls the Kestrel test server and InMemory database.
    /// </summary>
    protected PromptBankWebFactory Factory { get; }

    /// <summary>
    /// Gets the Playwright page used by the current test.
    /// A fresh page is created for every test method to ensure isolation.
    /// </summary>
    protected IPage Page { get; private set; } = null!;

    /// <summary>
    /// Gets the base URL of the running Kestrel server (e.g. <c>http://127.0.0.1:54321</c>).
    /// Available after <see cref="InitializeAsync"/> completes.
    /// </summary>
    protected string BaseUrl { get; private set; } = string.Empty;

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>
    /// Initialises a new instance of <see cref="E2ETestBase"/>.
    /// </summary>
    /// <param name="playwright">
    /// The shared Playwright fixture injected by xUnit; provides the <see cref="IBrowser"/>
    /// used to create per-test contexts.
    /// </param>
    protected E2ETestBase(PlaywrightFixture playwright)
    {
        _playwright = playwright;
        Factory = new PromptBankWebFactory();
    }

    // ── IAsyncLifetime ────────────────────────────────────────────────────────

    /// <summary>
    /// Called by xUnit before each test method.
    /// <list type="number">
    ///   <item>Forces the Kestrel test server to start (via <see cref="PromptBankWebFactory.ServerAddress"/>).</item>
    ///   <item>Creates an isolated <see cref="IBrowserContext"/> with clipboard read/write permissions.</item>
    ///   <item>Opens a new <see cref="IPage"/> within that context.</item>
    /// </list>
    /// </summary>
    /// <returns>A task that completes once the page is ready.</returns>
    public async Task InitializeAsync()
    {
        // Accessing ServerAddress triggers CreateHost, which starts the Kestrel server
        // and populates the internal server address.
        BaseUrl = Factory.ServerAddress.ToString().TrimEnd('/');

        _context = await _playwright.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = BaseUrl,
            Permissions = ["clipboard-read", "clipboard-write"]
        });

        Page = await _context.NewPageAsync();
    }

    /// <summary>
    /// Called by xUnit after each test method.
    /// Disposes the browser context and the web application factory (which stops Kestrel).
    /// </summary>
    /// <returns>A task that completes once all resources are released.</returns>
    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        Factory.Dispose();
    }

    /// <summary>
    /// Gets the user ID of the seeded "alice" test account. Use this as <c>OwnerId</c>
    /// when seeding prompts so FK constraints are satisfied.
    /// </summary>
    protected string TestUserId => Factory.AliceId;

    // ── Seeding helper ────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds the given <paramref name="prompts"/> into the isolated InMemory database used by
    /// the current test's Kestrel server.  Call this after <see cref="InitializeAsync"/> has run
    /// (i.e. from within a test method) and before navigating to any page.
    /// </summary>
    /// <param name="prompts">One or more <see cref="Prompt"/> entities to add.</param>
    /// <returns>A task that completes once all entities have been persisted.</returns>
    protected Task SeedAsync(params Prompt[] prompts) => Factory.SeedAsync(prompts);

    /// <summary>
    /// Seeds the given <paramref name="prompts"/> with real semantic embeddings so they
    /// participate in <c>SearchAsync</c> results.  Use this when testing the search feature.
    /// </summary>
    /// <param name="prompts">One or more <see cref="Prompt"/> entities to add with embeddings.</param>
    /// <returns>A task that completes once all entities have been persisted.</returns>
    protected Task SeedWithEmbeddingsAsync(params Prompt[] prompts) => Factory.SeedWithEmbeddingsAsync(prompts);

    // ── Authentication helper ─────────────────────────────────────────────────

    /// <summary>
    /// Logs in the given user via the <c>/Account/Login</c> page and waits until the
    /// browser is redirected back to the home page (<c>/</c>).
    /// </summary>
    /// <param name="username">The username of the account to log in as.</param>
    /// <param name="password">The account password.</param>
    /// <returns>A task that completes once the login redirect has settled.</returns>
    protected async Task LoginAsync(string username, string password)
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Page.FillAsync("#Input_Username", username);
        await Page.FillAsync("#Input_Password", password);
        await Page.ClickAsync("button[type='submit']");
        await Page.WaitForURLAsync(new System.Text.RegularExpressions.Regex("/$"));
    }
}
