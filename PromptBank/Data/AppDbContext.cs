using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PromptBank.Models;

namespace PromptBank.Data;

/// <summary>
/// Entity Framework Core database context for the Prompt Bank application.
/// </summary>
public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    /// <summary>
    /// Initialises a new instance of <see cref="AppDbContext"/> with the given options.
    /// </summary>
    /// <param name="options">The context options configured by the DI container.</param>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    /// <summary>Gets or sets the <see cref="DbSet{Prompt}"/> for all prompts.</summary>
    public DbSet<Prompt> Prompts { get; set; }

    /// <summary>Gets or sets the <see cref="DbSet{UserPromptPin}"/> for per-user pins.</summary>
    public DbSet<UserPromptPin> UserPromptPins { get; set; }

    /// <summary>Gets or sets the <see cref="DbSet{UserPromptRating}"/> for per-user ratings.</summary>
    public DbSet<UserPromptRating> UserPromptRatings { get; set; }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserPromptPin>()
            .HasKey(p => new { p.UserId, p.PromptId });

        modelBuilder.Entity<UserPromptRating>()
            .HasKey(r => new { r.UserId, r.PromptId });
    }
}
