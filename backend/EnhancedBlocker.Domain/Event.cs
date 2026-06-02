using OneOf;

namespace EnhancedBlocker.Domain;

/// <summary>
/// A single tracked browsing event (navigation / activity / idle). Feeds time tracking.
/// </summary>
public sealed class Event
{
    public Guid Id { get; private set; }
    public DateTimeOffset Ts { get; private set; }
    public string Url { get; private set; } = null!;
    public string Domain { get; private set; } = null!;
    public string? Title { get; private set; }
    public int TabId { get; private set; }
    public EventType Type { get; private set; }
    public Guid? FocusSessionId { get; private set; }
    public long? DurationMs { get; private set; }

    // EF Core materialization ctor.
    private Event() { }

    public static OneOf<Event, ValidationError> Create(
        DateTimeOffset ts,
        string url,
        string domain,
        string? title,
        int tabId,
        EventType type,
        Guid? focusSessionId,
        long? durationMs,
        Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(url))
            return ValidationError.Required(nameof(url));
        if (string.IsNullOrWhiteSpace(domain))
            return ValidationError.Required(nameof(domain));
        if (durationMs is < 0)
            return new ValidationError("durationMs cannot be negative.");

        return new Event
        {
            Id = id ?? Guid.NewGuid(),
            Ts = ts,
            Url = url.Trim(),
            Domain = domain.Trim().ToLowerInvariant(),
            Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
            TabId = tabId,
            Type = type,
            FocusSessionId = focusSessionId,
            DurationMs = durationMs
        };
    }
}
