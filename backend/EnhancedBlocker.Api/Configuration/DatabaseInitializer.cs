using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using EnhancedBlocker.Domain;
using EnhancedBlocker.Infrastructure.Persistence;

namespace EnhancedBlocker.Api.Configuration;

/// <summary>
/// Composition-root startup step, gated by <see cref="EnhancedBlockerOptions.AutoMigrate"/>:
/// applies EF migrations, then seeds Tier-0 rules from the seed file and a starter set of
/// categories — each only when its table is empty (so a fresh clone is usable without
/// manual steps, but existing data is never clobbered).
/// </summary>
internal static class DatabaseInitializer
{
    private static readonly JsonSerializerOptions SeedJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>Seed-file entry. <c>Source</c> is always <see cref="RuleSource.Manual"/>.</summary>
    private sealed record SeedRule(string Pattern, MatchKind Match, RuleKind Kind, string? Category);

    /// <summary>Starter categories, seeded only when the table is empty.</summary>
    private static readonly string[] DefaultCategories = ["news", "social media", "brainrot"];

    public static async Task MigrateAndSeedAsync(WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<EnhancedBlockerOptions>>().Value;
        if (!options.AutoMigrate)
            return;

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();
        await SeedRulesAsync(app, db, options);
        await SeedCategoriesAsync(app, db);
    }

    private static async Task SeedRulesAsync(WebApplication app, AppDbContext db, EnhancedBlockerOptions options)
    {
        if (await db.Rules.AnyAsync())
            return;

        var seedPath = Path.Combine(app.Environment.ContentRootPath, options.SeedRulesFile);
        if (!File.Exists(seedPath))
        {
            app.Logger.LogInformation("No seed file at {Path}; Rules table left empty.", seedPath);
            return;
        }

        await using var stream = File.OpenRead(seedPath);
        var entries = await JsonSerializer.DeserializeAsync<List<SeedRule>>(stream, SeedJson) ?? [];

        foreach (var entry in entries)
        {
            var created = Rule.Create(entry.Pattern, entry.Match, entry.Kind, RuleSource.Manual, entry.Category);
            if (created.IsT1)
                throw new InvalidOperationException(
                    $"Invalid seed rule '{entry.Pattern}' in {options.SeedRulesFile}: {created.AsT1.Message}");

            db.Rules.Add(created.AsT0);
        }

        await db.SaveChangesAsync();
        app.Logger.LogInformation("Seeded {Count} Tier-0 rules from {File}.", entries.Count, options.SeedRulesFile);
    }

    private static async Task SeedCategoriesAsync(WebApplication app, AppDbContext db)
    {
        if (await db.Categories.AnyAsync())
            return;

        foreach (var name in DefaultCategories)
        {
            var created = Category.Create(name);
            if (created.IsT0)
                db.Categories.Add(created.AsT0);
        }

        await db.SaveChangesAsync();
        app.Logger.LogInformation("Seeded {Count} default categories.", DefaultCategories.Length);
    }
}
