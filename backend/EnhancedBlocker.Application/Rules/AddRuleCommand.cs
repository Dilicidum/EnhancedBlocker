using OneOf;
using EnhancedBlocker.Application.Ports;
using EnhancedBlocker.Domain;

namespace EnhancedBlocker.Application.Rules;

/// <summary>Adds a Tier-0 rule. Returns the created rule.</summary>
public sealed record AddRuleCommand(
    string Pattern,
    MatchKind Match,
    RuleKind Kind,
    RuleSource Source,
    string? Category);

public sealed class AddRuleCommandHandler(IRuleRepository repository)
{
    public async Task<OneOf<Rule, ValidationError>> Handle(AddRuleCommand request, CancellationToken ct)
    {
        var created = Rule.Create(
            request.Pattern, request.Match, request.Kind, request.Source, request.Category);

        if (created.IsT1)
            return created.AsT1;

        var rule = created.AsT0;
        await repository.AddAsync(rule, ct);
        return rule;
    }
}
