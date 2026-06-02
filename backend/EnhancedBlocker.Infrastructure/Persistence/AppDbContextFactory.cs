using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EnhancedBlocker.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> (e.g. for migrations) when it cannot build the
/// runtime host. The connection string only needs to be valid enough to scaffold/apply migrations;
/// the Npgsql provider must be configured so the migration generator targets PostgreSQL.
/// Override via the <c>EB_CONNECTION_STRING</c> environment variable.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("EB_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=enhancedblocker;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
