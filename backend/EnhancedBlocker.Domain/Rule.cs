using OneOf;

namespace EnhancedBlocker.Domain;

/// <summary>
/// A Tier-0 hard rule: a pattern (exact URL or domain) that blocks or explicitly allows.
/// </summary>
public sealed class Rule
{
    public Guid Id { get; private set; }
    public string Pattern { get; private set; } = null!;
    public MatchKind Match { get; private set; }
    public RuleKind Kind { get; private set; }
    public RuleSource Source { get; private set; }
    public string? Category { get; private set; }

    // EF Core materialization ctor.
    private Rule() { }

    public static OneOf<Rule, ValidationError> Create(
        string pattern,
        MatchKind match,
        RuleKind kind,
        RuleSource source,
        string? category,
        Guid? id = null)
    {
        var validated = Validate(pattern, match, category);
        if (validated.IsT1)
            return validated.AsT1;

        var (normalizedPattern, normalizedCategory) = validated.AsT0;

        return new Rule
        {
            Id = id ?? Guid.NewGuid(),
            Pattern = normalizedPattern,
            Match = match,
            Kind = kind,
            Source = source,
            Category = normalizedCategory
        };
    }

    public OneOf<Rule, ValidationError> Update(
        string pattern,
        MatchKind match,
        RuleKind kind,
        RuleSource source,
        string? category)
    {
        var validated = Validate(pattern, match, category);
        if (validated.IsT1)
            return validated.AsT1;

        var (normalizedPattern, normalizedCategory) = validated.AsT0;

        Pattern = normalizedPattern;
        Match = match;
        Kind = kind;
        Source = source;
        Category = normalizedCategory;
        return this;
    }

    private static OneOf<(string Pattern, string? Category), ValidationError> Validate(
        string pattern,
        MatchKind match,
        string? category)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return ValidationError.Required(nameof(pattern));

        var normalized = pattern.Trim();
        // Domain patterns are case-insensitive; normalize to lowercase for stable matching.
        if (match == MatchKind.Domain)
            normalized = normalized.ToLowerInvariant();

        var normalizedCategory = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        return (normalized, normalizedCategory);
    }
}
