namespace EnhancedBlocker.Domain;

/// <summary>
/// Returned by domain factories (inside a <c>OneOf</c>) when an invariant is violated.
/// Keeps validation failures out of the exception path.
/// </summary>
public sealed record ValidationError(string Message)
{
    public static ValidationError Required(string field) =>
        new($"{field} is required.");
}
