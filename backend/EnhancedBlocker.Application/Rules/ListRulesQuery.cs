using EnhancedBlocker.Application.Ports;
using EnhancedBlocker.Domain;

namespace EnhancedBlocker.Application.Rules;

/// <summary>Lists all Tier-0 rules.</summary>
public sealed record ListRulesQuery;

public sealed class ListRulesQueryHandler(IRuleRepository repository)
{
    public Task<IReadOnlyList<Rule>> Handle(ListRulesQuery request, CancellationToken ct) =>
        repository.ListAsync(ct);
}
