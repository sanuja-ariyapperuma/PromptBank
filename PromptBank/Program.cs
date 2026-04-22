using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PromptBank.Data;
using PromptBank.Models;
using PromptBank.Services;
using SmartComponents.LocalEmbeddings;

var builder = WebApplication.CreateBuilder(args);

// Razor Pages (antiforgery enabled by default)
builder.Services.AddRazorPages();

// EF Core with SQLite — ensure the data directory exists (required on App Service /home mount)
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    var dataSource = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString).DataSource;
    if (!string.IsNullOrEmpty(dataSource) && !string.Equals(dataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(dataSource));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }
    options.UseSqlite(connectionString);
});

// ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// Configure authentication cookie
builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Account/Login";
    o.SlidingExpiration = true;
    o.ExpireTimeSpan = TimeSpan.FromDays(7);
});

// Business logic
builder.Services.AddScoped<IPromptService, PromptService>();

// Semantic search
builder.Services.AddSingleton<LocalEmbedder>();
builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();

// Backfill missing embeddings in the background after startup (keeps ONNX load off the critical path)
builder.Services.AddHostedService<EmbeddingBackfillService>();
builder.Services.Configure<SearchOptions>(builder.Configuration.GetSection("Search"));

// Rate limiting for AJAX endpoints
builder.Services.AddRateLimiter(opts =>
    opts.AddFixedWindowLimiter("ajax", o =>
    {
        o.PermitLimit = 30;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueLimit = 0;
    }));

var app = builder.Build();

// Auto-apply EF migrations and seed data on startup.
// When Test:SkipDatabaseInit=true the test factory initialises the database itself
// (once, via EnsureCreated) to avoid double-initialisation across the two host
// instances (test host + Kestrel host) that share the same named in-memory database.
if (!app.Configuration.GetValue<bool>("Test:SkipDatabaseInit"))
{
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsRelational())
        await db.Database.MigrateAsync();
    else
        await db.Database.EnsureCreatedAsync();

    // Seed users
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var seedUsers = new[] {
        ("alice", "Alice@1234"),
        ("bob",   "Bob@1234"),
        ("carol", "Carol@1234"),
    };
    foreach (var (name, pass) in seedUsers)
    {
        if (await userManager.FindByNameAsync(name) is null)
            await userManager.CreateAsync(new ApplicationUser { UserName = name, Email = $"{name}@promptbank.local" }, pass);
    }

    // Seed prompts (only if none exist)
    var dbForSeed = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (!dbForSeed.Prompts.Any())
    {
        var alice = await userManager.FindByNameAsync("alice");
        var bob   = await userManager.FindByNameAsync("bob");
        var carol = await userManager.FindByNameAsync("carol");
        var prompts = new List<Prompt>
        {
            new() { Title = "Explain code",           Description = "Use this prompt to get a plain-English walkthrough of any code snippet, ideal for onboarding or code review.", Content = "Explain the following code step by step...\n\n```\n{{code}}\n```",          OwnerId = alice!.Id, RatingTotal = 45, RatingCount = 10, CreatedAt = new DateTime(2025,1,1,0,0,0,DateTimeKind.Utc) },
            new() { Title = "Write unit tests",       Description = "Generates xUnit unit tests for a given method, covering happy paths and edge cases.", Content = "Write xUnit unit tests for the following method...\n\n```\n{{method}}\n```", OwnerId = bob!.Id,   RatingTotal = 38, RatingCount = 9,  CreatedAt = new DateTime(2025,2,1,0,0,0,DateTimeKind.Utc) },
            new() { Title = "Code review checklist",  Description = "Runs a structured review of code for correctness, security vulnerabilities, and maintainability issues.", Content = "Review the following code for correctness, security...\n\n```\n{{code}}\n```", OwnerId = carol!.Id, RatingTotal = 20, RatingCount = 5, CreatedAt = new DateTime(2025,3,1,0,0,0,DateTimeKind.Utc) },
            new() { Title = "Generate SQL query",     Description = "Produces a SQL query from a natural-language description and a provided schema, saving time on boilerplate.", Content = "Write a SQL query to {{task}}. Schema:\n\n{{schema}}",                      OwnerId = alice!.Id, RatingTotal = 12, RatingCount = 4,  CreatedAt = new DateTime(2025,4,1,0,0,0,DateTimeKind.Utc) },
            new() { Title = "Summarise PR",           Description = "Creates a concise plain-English summary of a pull request diff, useful for release notes or review kick-offs.", Content = "Summarise the following PR diff in plain English:\n\n{{diff}}",             OwnerId = bob!.Id,   RatingTotal = 8,  RatingCount = 3,  CreatedAt = new DateTime(2025,5,1,0,0,0,DateTimeKind.Utc) },
        };
        dbForSeed.Prompts.AddRange(prompts);
        await dbForSeed.SaveChangesAsync();
        // Pin: alice pins "Explain code", bob pins "Write unit tests"
        dbForSeed.UserPromptPins.Add(new UserPromptPin { UserId = alice!.Id, PromptId = prompts[0].Id });
        dbForSeed.UserPromptPins.Add(new UserPromptPin { UserId = bob!.Id,   PromptId = prompts[1].Id });
        await dbForSeed.SaveChangesAsync();
    }
    else
    {
        // Backfill descriptions for any prompts that pre-date this field
        var descriptionMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Explain code"]           = "Use this prompt to get a plain-English walkthrough of any code snippet, ideal for onboarding or code review.",
            ["Write unit tests"]       = "Generates xUnit unit tests for a given method, covering happy paths and edge cases.",
            ["Code review checklist"]  = "Runs a structured review of code for correctness, security vulnerabilities, and maintainability issues.",
            ["Generate SQL query"]     = "Produces a SQL query from a natural-language description and a provided schema, saving time on boilerplate.",
            ["Summarise PR"]           = "Creates a concise plain-English summary of a pull request diff, useful for release notes or review kick-offs.",
        };
        var undescribed = dbForSeed.Prompts.Where(p => p.Description == "").ToList();
        foreach (var p in undescribed)
        {
            if (descriptionMap.TryGetValue(p.Title, out var desc))
                p.Description = desc;
        }
        if (undescribed.Any(p => p.Description != ""))
            await dbForSeed.SaveChangesAsync();
    }

    // Backfill embeddings handled by EmbeddingBackfillService (runs after startup)
}
} // end if (!Test:SkipDatabaseInit)

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.Run();

// Expose Program for WebApplicationFactory in integration/E2E tests
public partial class Program { }
