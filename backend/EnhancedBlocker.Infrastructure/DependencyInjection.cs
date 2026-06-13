using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EnhancedBlocker.Application.Ports;
using EnhancedBlocker.Infrastructure.Decisions;
using EnhancedBlocker.Infrastructure.Persistence;
using EnhancedBlocker.Infrastructure.Repositories;

namespace EnhancedBlocker.Infrastructure;

/// <summary>Wires Infrastructure adapters into the Application ports.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IRuleRepository, RuleRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IEventStore, EventStore>();
        services.AddScoped<IFocusSessionRepository, FocusSessionRepository>();
        services.AddScoped<ILabelStore, LabelStore>();
        services.AddScoped<ICategoryDomainCache, CategoryDomainCache>();

        // M1 registers Tier 0 as the only decision tier. M2 appends Tier1MlTier here.
        services.AddScoped<IDecisionTier, Tier0RuleTier>();

        return services;
    }
}
