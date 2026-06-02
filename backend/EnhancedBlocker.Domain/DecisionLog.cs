using OneOf;

namespace EnhancedBlocker.Domain;

/// <summary>
/// Audit record of a cascade decision: which tier fired and what it concluded.
/// For debugging/auditing the cascade.
/// </summary>
public sealed class DecisionLog
{
    public Guid Id { get; private set; }
    public DateTimeOffset Ts { get; private set; }
    public string Url { get; private set; } = null!;
    public string Tier { get; private set; } = null!;
    public string Outcome { get; private set; } = null!;
    public double? Score { get; private set; }

    // EF Core materialization ctor.
    private DecisionLog() { }

    public static OneOf<DecisionLog, ValidationError> Create(
        DateTimeOffset ts,
        string url,
        string tier,
        string outcome,
        double? score,
        Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(url))
            return ValidationError.Required(nameof(url));
        if (string.IsNullOrWhiteSpace(tier))
            return ValidationError.Required(nameof(tier));
        if (string.IsNullOrWhiteSpace(outcome))
            return ValidationError.Required(nameof(outcome));

        return new DecisionLog
        {
            Id = id ?? Guid.NewGuid(),
            Ts = ts,
            Url = url.Trim(),
            Tier = tier.Trim(),
            Outcome = outcome.Trim(),
            Score = score
        };
    }
}
