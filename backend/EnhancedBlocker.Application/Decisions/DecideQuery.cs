using OneOf;
using EnhancedBlocker.Application.Messaging;
using EnhancedBlocker.Application.Ports;
using EnhancedBlocker.Domain;

namespace EnhancedBlocker.Application.Decisions;

/// <summary>Runs the decision cascade for a page. Returns the deciding <see cref="TierResult"/>.</summary>
public sealed record DecideQuery(DecisionContext Context)
    : IRequest<OneOf<TierResult, ValidationError>>;

/// <summary>
/// Ordered tier cascade: the first tier that returns a <see cref="TierResult"/> (rather than
/// <see cref="Defer"/>) wins. If every tier defers, the default is Allow.
/// </summary>
public sealed class DecideQueryHandler(IEnumerable<IDecisionTier> tiers)
    : IRequestHandler<DecideQuery, OneOf<TierResult, ValidationError>>
{
    private readonly IReadOnlyList<IDecisionTier> _tiers =
        tiers.OrderBy(t => t.Order).ToList();

    public async Task<OneOf<TierResult, ValidationError>> Handle(DecideQuery request, CancellationToken ct)
    {
        var ctx = request.Context;
        if (string.IsNullOrWhiteSpace(ctx.Url))
            return ValidationError.Required(nameof(ctx.Url));

        foreach (var tier in _tiers)
        {
            var result = await tier.EvaluateAsync(ctx, ct);
            if (result.IsT0)
                return result.AsT0;
        }

        return new TierResult(Outcome.Allow, "default", "no rule matched", null);
    }
}
