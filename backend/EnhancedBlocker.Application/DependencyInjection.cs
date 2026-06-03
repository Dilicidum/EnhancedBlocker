using Microsoft.Extensions.DependencyInjection;
using EnhancedBlocker.Application.Decisions;
using EnhancedBlocker.Application.Events;
using EnhancedBlocker.Application.Feedback;
using EnhancedBlocker.Application.Focus;
using EnhancedBlocker.Application.Rules;

namespace EnhancedBlocker.Application;

/// <summary>Registers every CQRS handler in this assembly for direct injection.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<LogEventsCommandHandler>();
        services.AddScoped<DecideQueryHandler>();
        services.AddScoped<RecordFeedbackCommandHandler>();
        services.AddScoped<ListRulesQueryHandler>();
        services.AddScoped<AddRuleCommandHandler>();
        services.AddScoped<DeleteRuleCommandHandler>();
        services.AddScoped<StartFocusCommandHandler>();
        services.AddScoped<StopFocusCommandHandler>();

        return services;
    }
}
