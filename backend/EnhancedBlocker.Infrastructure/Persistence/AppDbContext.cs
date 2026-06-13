using Microsoft.EntityFrameworkCore;
using EnhancedBlocker.Domain;

namespace EnhancedBlocker.Infrastructure.Persistence;

/// <summary>EF Core context for the EnhancedBlocker PostgreSQL database.</summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Event> Events => Set<Event>();
    public DbSet<FocusSession> FocusSessions => Set<FocusSession>();
    public DbSet<Rule> Rules => Set<Rule>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<CategoryDomain> CategoryDomains => Set<CategoryDomain>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<DecisionLog> DecisionLogs => Set<DecisionLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
