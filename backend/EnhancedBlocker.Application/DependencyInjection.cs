using Microsoft.Extensions.DependencyInjection;
using OneOf;
using EnhancedBlocker.Application.Decisions;
using EnhancedBlocker.Application.Events;
using EnhancedBlocker.Application.Feedback;
using EnhancedBlocker.Application.Focus;
using EnhancedBlocker.Application.Messaging;
using EnhancedBlocker.Application.Rules;
using EnhancedBlocker.Domain;

namespace EnhancedBlocker.Application;

/// <summary>Registers the dispatcher and every CQRS handler in this assembly.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ISender, Sender>();

        services.AddScoped<IRequestHandler<LogEventsCommand, OneOf<int, ValidationError>>, LogEventsCommandHandler>();
        services.AddScoped<IRequestHandler<DecideQuery, OneOf<TierResult, ValidationError>>, DecideQueryHandler>();
        services.AddScoped<IRequestHandler<RecordFeedbackCommand, OneOf<Guid, ValidationError>>, RecordFeedbackCommandHandler>();
        services.AddScoped<IRequestHandler<ListRulesQuery, IReadOnlyList<Rule>>, ListRulesQueryHandler>();
        services.AddScoped<IRequestHandler<AddRuleCommand, OneOf<Rule, ValidationError>>, AddRuleCommandHandler>();
        services.AddScoped<IRequestHandler<DeleteRuleCommand, bool>, DeleteRuleCommandHandler>();
        services.AddScoped<IRequestHandler<StartFocusCommand, OneOf<Guid, ValidationError>>, StartFocusCommandHandler>();
        services.AddScoped<IRequestHandler<StopFocusCommand, OneOf<Guid, ValidationError>>, StopFocusCommandHandler>();

        return services;
    }
}
