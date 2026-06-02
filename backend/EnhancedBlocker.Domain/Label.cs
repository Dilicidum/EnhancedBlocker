using OneOf;

namespace EnhancedBlocker.Domain;

/// <summary>
/// A feedback label (allow/block) gathered from the block screen or active learning.
/// Feeds the M2+ learned combiner. <see cref="FeaturesJson"/> is an M2 seam (stays null in M1).
/// </summary>
public sealed class Label
{
    public Guid Id { get; private set; }
    public DateTimeOffset Ts { get; private set; }
    public string Url { get; private set; } = null!;
    public string? Title { get; private set; }
    public Decision Decision { get; private set; }
    public LabelSource Source { get; private set; }

    /// <summary>M2 seam: serialized Tier-1 features (PostgreSQL <c>jsonb</c>). Null in M1.</summary>
    public string? FeaturesJson { get; private set; }

    // EF Core materialization ctor.
    private Label() { }

    public static OneOf<Label, ValidationError> Create(
        DateTimeOffset ts,
        string url,
        string? title,
        Decision decision,
        LabelSource source,
        Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(url))
            return ValidationError.Required(nameof(url));

        return new Label
        {
            Id = id ?? Guid.NewGuid(),
            Ts = ts,
            Url = url.Trim(),
            Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
            Decision = decision,
            Source = source,
            FeaturesJson = null
        };
    }
}
