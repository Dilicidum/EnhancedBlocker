using OneOf;
using EnhancedBlocker.Application.Decisions;
using EnhancedBlocker.Application.Ports;
using EnhancedBlocker.Domain;

namespace EnhancedBlocker.Tests.Fakes;

public sealed class FakeRuleRepository(IEnumerable<Rule>? rules = null) : IRuleRepository
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

public sealed class FakeCategoryDomainCache(IDictionary<string, CategoryDomain>? entries = null)
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

public sealed class FakeEventStore : IEventStore
{
    public List<Event> Events { get; } = [];

    public Task AddRangeAsync(IEnumerable<Event> events, CancellationToken ct)
    {
        Events.AddRange(events);
        return Task.CompletedTask;
    }
}

public sealed class FakeLabelStore : ILabelStore
{
    public List<Label> Labels { get; } = [];

    public Task AddAsync(Label label, CancellationToken ct)
    {
        Labels.Add(label);
        return Task.CompletedTask;
    }
}

public sealed class FakeFocusSessionRepository : IFocusSessionRepository
{
    public List<FocusSession> Sessions { get; } = [];

    public Task AddAsync(FocusSession session, CancellationToken ct)
    {
        Sessions.Add(session);
        return Task.CompletedTask;
    }

    public Task<FocusSession?> GetAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Sessions.FirstOrDefault(s => s.Id == id));

    public Task<FocusSession?> GetActiveAsync(CancellationToken ct) =>
        Task.FromResult(Sessions
            .Where(s => s.EndedAt is null)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefault());

    public Task UpdateAsync(FocusSession session, CancellationToken ct) => Task.CompletedTask;
}
