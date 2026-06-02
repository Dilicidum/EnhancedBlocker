using OneOf;

namespace EnhancedBlocker.Domain;

/// <summary>
/// Tier-0 category cache entry: a domain mapped to a category with a confidence.
/// Promotes (M2) ML category discoveries into cheap deterministic Tier-0 enforcement.
/// Keyed by <see cref="Domain"/>.
/// </summary>
public sealed class CategoryDomain
{
    public string Domain { get; private set; } = null!;
    public string Category { get; private set; } = null!;
    public double Confidence { get; private set; }
    public DateTimeOffset AddedAt { get; private set; }

    // EF Core materialization ctor.
    private CategoryDomain() { }

    public static OneOf<CategoryDomain, ValidationError> Create(
        string domain,
        string category,
        double confidence,
        DateTimeOffset addedAt)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return ValidationError.Required(nameof(domain));
        if (string.IsNullOrWhiteSpace(category))
            return ValidationError.Required(nameof(category));
        if (confidence is < 0.0 or > 1.0)
            return new ValidationError("confidence must be between 0 and 1.");

        return new CategoryDomain
        {
            Domain = domain.Trim().ToLowerInvariant(),
            Category = category.Trim(),
            Confidence = confidence,
            AddedAt = addedAt
        };
    }

    public OneOf<CategoryDomain, ValidationError> Update(double confidence, DateTimeOffset addedAt)
    {
        if (confidence is < 0.0 or > 1.0)
            return new ValidationError("confidence must be between 0 and 1.");

        Confidence = confidence;
        AddedAt = addedAt;
        return this;
    }
}
