using OneOf;
using EnhancedBlocker.Application.Decisions;
using EnhancedBlocker.Application.Ports;
using EnhancedBlocker.Domain;

namespace EnhancedBlocker.Tests.Fakes;

internal sealed class FakeRuleRepository(IEnumerable<Rule>? rules = null) : IRuleRepository
{
    private readonly List<Rule> _rules = rules?.ToList() ?? [];

    public Task<IReadOnlyList<Rule>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Rule>>(_rules.ToList());

    public Task<Rule?> GetAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_rules.FirstOrDefault(r => r.Id == id));

    public Task AddAsync(Rule rule, CancellationToken ct)
    {
        _rules.Add(rule);
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var found = _rules.FirstOrDefault(r => r.Id == id);
        if (found is null)
            return Task.FromResult(false);
        _rules.Remove(found);
        return Task.FromResult(true);
    }
}

internal sealed class FakeCategoryDomainCache(IDictionary<string, CategoryDomain>? entries = null)
    : ICategoryDomainCache
{
    private readonly IDictionary<string, CategoryDomain> _entries =
        entries ?? new Dictionary<string, CategoryDomain>();

    public Task<CategoryDomain?> GetAsync(string domain, CancellationToken ct) =>
        Task.FromResult(_entries.TryGetValue(domain.ToLowerInvariant(), out var c) ? c : null);
}

/// <summary>A tier that always returns the configured result; used to test cascade ordering.</summary>
internal sealed class StubTier(int order, OneOf<TierResult, Defer> result) : IDecisionTier
{
    public int Order => order;
    public Task<OneOf<TierResult, Defer>> EvaluateAsync(DecisionContext ctx, CancellationToken ct) =>
        Task.FromResult(result);
}
