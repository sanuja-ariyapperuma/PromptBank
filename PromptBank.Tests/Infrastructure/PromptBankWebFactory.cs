using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PromptBank.Data;
using PromptBank.Models;
using PromptBank.Services;

namespace PromptBank.Tests.Infrastructure;

/// <summary>
/// A <see cref="WebApplicationFactory{TEntryPoint}"/> specialisation for the Prompt Bank app that:
/// <list type="bullet">
///   <item>Replaces the production SQLite file database with a named SQLite in-memory database
///         (same provider, same SQL semantics — identical to production).</item>
///   <item>Starts a real Kestrel listener on a random loopback port so Playwright can reach it over HTTP.</item>
///   <item>Exposes <see cref="ServerAddress"/> (the bound URL) and a <see cref="SeedAsync"/> helper.</item>
/// </list>
/// <para>
/// Using a real SQLite provider (rather than EF Core InMemory) means that SQL translation,
/// ORDER BY behaviour, and type-mapping quirks are caught in tests — not just at runtime.
/// </para>
/// </summary>
public sealed class PromptBankWebFactory : WebApplicationFactory<Program>
{
    // Unique name for this factory's isolated in-memory SQLite database.
    // Both Kestrel and TestServer hosts connect to the same named DB via shared cache.
    private readonly string _dbName = Guid.NewGuid().ToString("N");

    // Holds the in-memory SQLite database alive for the full factory lifetime.
    // SQLite destroys a :memory: database the moment all connections to it close,
    // so this sentinel connection must remain open until Dispose().
    private readonly SqliteConnection _keepAliveConnection;

    // Connection string shared by both hosts — named in-memory + shared cache lets
    // multiple connection-pool connections all see the same database.
    private readonly string _connectionString;

    // Populated inside CreateHost once alice's user ID is known.
    private string _aliceId = string.Empty;

    /// <summary>Gets the user ID of the seeded "alice" account, available after the server starts.</summary>
    public string AliceId => _aliceId;

    // The real Kestrel host that Playwright navigates to.
    private IHost? _kestrelHost;

    // Populated inside CreateHost once the Kestrel server has bound to a port.
    private Uri? _serverAddress;

    /// <summary>
    /// Initialises the factory and opens the sentinel SQLite connection that keeps
    /// the named in-memory database alive.
    /// </summary>
    public PromptBankWebFactory()
    {
        _connectionString = $"DataSource=file:{_dbName}?mode=memory&cache=shared";
        _keepAliveConnection = new SqliteConnection(_connectionString);
        _keepAliveConnection.Open();
    }

    // ── Public surface ────────────────────────────────────────────────────────

    /// <summary>
    /// Gets the base URL (e.g. <c>http://127.0.0.1:54321</c>) of the real Kestrel server.
    /// Accessing this property forces the server to start if it has not already done so.
    /// </summary>
    public Uri ServerAddress
    {
        get
        {
            // Accessing Server triggers CreateServer → CreateHost, which populates _serverAddress.
            _ = Server;
            return _serverAddress ?? throw new InvalidOperationException("Kestrel host has not been started.");
        }
    }

    // ── WebApplicationFactory overrides ──────────────────────────────────────

    /// <summary>
    /// Replaces the production SQLite file connection string with a named SQLite in-memory
    /// connection string so the test server uses the same SQL provider as production.
    /// </summary>
    /// <param name="builder">The web host builder provided by the factory.</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Point the content root at the actual PromptBank app directory so that
        // UseStaticFiles() can find wwwroot/js/site.js and wwwroot/css/site.css.
        // AppContext.BaseDirectory is e.g. PromptBank.Tests\bin\Debug\net10.0\
        var testBinDir  = AppContext.BaseDirectory;
        var solutionDir = Path.GetFullPath(Path.Combine(testBinDir, "..", "..", "..", ".."));
        var appRoot     = Path.Combine(solutionDir, "PromptBank");
        builder.UseContentRoot(appRoot);

        // Use "Development" so that WebApplication.CreateBuilder automatically loads
        // the static web assets manifest, enabling MapStaticAssets() to serve site.js.
        builder.UseEnvironment("Development");

        // Signal Program.cs to skip all database initialisation (schema creation, user
        // seeding, and prompt seeding).  CreateHost runs EnsureCreated + user seeding
        // exactly once, avoiding the double-init failure that occurs when two host
        // instances (test host + Kestrel host) share the same named in-memory database.
        builder.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Test:SkipDatabaseInit"] = "true" }));

        builder.ConfigureServices(services =>
        {
            // EF Core 9+ registers IDbContextOptionsConfiguration<T> in addition to
            // DbContextOptions<T>.  Both must be removed before re-registering so that
            // no stale connection-string configuration survives from the production registration.
            var toRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                         || d.ServiceType == typeof(IDbContextOptionsConfiguration<AppDbContext>))
                .ToList();
            foreach (var d in toRemove)
                services.Remove(d);

            // Re-register using the same SQLite provider as production, but pointing at the
            // named in-memory database.  SQL translation, type mapping, and ORDER BY behaviour
            // are therefore identical to the real app.
            services.AddDbContext<AppDbContext>(opts =>
                opts.UseSqlite(_connectionString));
        });
    }

    /// <summary>
    /// Overrides the default host creation to spin up two hosts:
    /// <list type="number">
    ///   <item>An in-process <em>test host</em> returned to the WebApplicationFactory infrastructure.</item>
    ///   <item>A real <em>Kestrel host</em> on a random loopback port for Playwright.</item>
    /// </list>
    /// Both hosts share the same named SQLite in-memory database (shared-cache connection string),
    /// so data seeded via <see cref="SeedAsync"/> is visible to Playwright HTTP requests.
    /// </summary>
    /// <param name="builder">The host builder already configured by the factory infrastructure.</param>
    /// <returns>The in-process test host (handed back to the factory).</returns>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        // ── 1. Build the in-process host (TestServer) ──────────────────────
        var testHost = builder.Build();

        // ── 2. Build the real Kestrel host ─────────────────────────────────
        builder.ConfigureWebHost(webHostBuilder =>
            webHostBuilder.UseKestrel(opts =>
                opts.Listen(IPAddress.Loopback, port: 0)));

        _kestrelHost = builder.Build();
        _kestrelHost.Start();

        var server = _kestrelHost.Services.GetRequiredService<IServer>();
        var addressesFeature = server.Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("IServerAddressesFeature is not available.");
        _serverAddress = new Uri(addressesFeature.Addresses.First());

        // ── Initialise schema and seed test users ─────────────────────────
        // Program.cs skips all DB init when Test:SkipDatabaseInit=true.
        // We do it once here so both the test host and Kestrel host share the
        // same already-initialised named in-memory SQLite database.
        using (var scope = _kestrelHost.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();

            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            foreach (var (name, pass) in new[] {
                ("alice", "Alice@1234"),
                ("bob",   "Bob@1234"),
                ("carol", "Carol@1234"),
            })
            {
                if (userManager.FindByNameAsync(name).GetAwaiter().GetResult() is null)
                    userManager.CreateAsync(
                        new ApplicationUser { UserName = name, Email = $"{name}@promptbank.local" },
                        pass
                    ).GetAwaiter().GetResult();
            }

            var alice = db.Users.FirstOrDefault(u => u.UserName == "alice");
            _aliceId = alice?.Id ?? string.Empty;
        }
        // ── 3. Return the test host to WebApplicationFactory ────────────────
        return testHost;
    }

    // ── Seeding helper ────────────────────────────────────────────────────────

    /// <summary>
    /// Adds the supplied <paramref name="prompts"/> to the SQLite in-memory database used by
    /// the Kestrel host that Playwright's requests are routed to.
    /// </summary>
    /// <param name="prompts">One or more <see cref="Prompt"/> entities to seed.</param>
    /// <returns>A task that completes when all prompts have been persisted.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the Kestrel host has not been started.</exception>
    public async Task SeedAsync(params Prompt[] prompts)
    {
        _ = Server;

        if (_kestrelHost is null)
            throw new InvalidOperationException("Kestrel host is not running. Call SeedAsync after InitializeAsync.");

        await using var scope = _kestrelHost.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Prompts.AddRange(prompts);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Adds the supplied <paramref name="prompts"/> to the database, computing a semantic
    /// embedding for each one using the real <see cref="IEmbeddingService"/>.
    /// Use this overload when the test needs to exercise <see cref="IPromptService.SearchAsync"/>,
    /// which filters out prompts with a <c>null</c> <see cref="Prompt.TitleDescriptionEmbedding"/>.
    /// </summary>
    /// <param name="prompts">One or more <see cref="Prompt"/> entities to seed with embeddings.</param>
    /// <returns>A task that completes when all prompts have been persisted.</returns>
    public async Task SeedWithEmbeddingsAsync(params Prompt[] prompts)
    {
        _ = Server;

        if (_kestrelHost is null)
            throw new InvalidOperationException("Kestrel host is not running. Call SeedAsync after InitializeAsync.");

        await using var scope = _kestrelHost.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();

        foreach (var p in prompts)
            p.TitleDescriptionEmbedding = embeddingService.GetEmbeddingBytes($"{p.Title} {p.Description}");

        db.Prompts.AddRange(prompts);
        await db.SaveChangesAsync();
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Stops the Kestrel host, closes the sentinel SQLite connection (allowing the
    /// in-memory database to be reclaimed), and performs standard factory cleanup.
    /// </summary>
    /// <param name="disposing">Whether managed resources should be released.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_kestrelHost is not null)
            {
                _kestrelHost.StopAsync().GetAwaiter().GetResult();
                _kestrelHost.Dispose();
            }

            // Close the sentinel connection — this releases the named in-memory database.
            _keepAliveConnection.Dispose();
        }

        base.Dispose(disposing);
    }
}

