using OneOf;
using EnhancedBlocker.Application.Decisions;
using EnhancedBlocker.Domain;

namespace EnhancedBlocker.Application.Ports;

/// <summary>Persistence for Tier-0 <see cref="Rule"/>s.</summary>
public interface IRuleRepository
{
    Task<IReadOnlyList<Rule>> ListAsync(CancellationToken ct);
    Task<Rule?> GetAsync(Guid id, CancellationToken ct);
    Task AddAsync(Rule rule, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct);
}

/// <summary>Append-only store for tracked <see cref="Event"/>s.</summary>
public interface IEventStore
{
    Task AddRangeAsync(IEnumerable<Event> events, CancellationToken ct);
}

/// <summary>Persistence for <see cref="FocusSession"/>s.</summary>
public interface IFocusSessionRepository
{
    Task AddAsync(FocusSession session, CancellationToken ct);
    Task<FocusSession?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>The most recent session that has not yet ended, if any.</summary>
    Task<FocusSession?> GetActiveAsync(CancellationToken ct);

    Task UpdateAsync(FocusSession session, CancellationToken ct);
}

/// <summary>Persistence for feedback <see cref="Label"/>s.</summary>
public interface ILabelStore
{
    Task AddAsync(Label label, CancellationToken ct);
}

/// <summary>Read access to the Tier-0 <see cref="CategoryDomain"/> cache.</summary>
public interface ICategoryDomainCache
{
    Task<CategoryDomain?> GetAsync(string domain, CancellationToken ct);
}

/// <summary>
/// One stage of the decision cascade. Returns a decisive <see cref="TierResult"/> or
/// <see cref="Defer"/> to fall through to the next tier. M1 registers only Tier 0.
/// </summary>
public interface IDecisionTier
{
    /// <summary>Lower runs first.</summary>
    int Order { get; }

    Task<OneOf<TierResult, Defer>> EvaluateAsync(DecisionContext ctx, CancellationToken ct);
}
