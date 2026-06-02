using OneOf;

namespace EnhancedBlocker.Domain;

/// <summary>
/// A declared focus session ("am I working?" signal) carrying a short intent.
/// <see cref="IntentEmbedding"/> is an M2 seam (stays null in M1).
/// </summary>
public sealed class FocusSession
{
    public Guid Id { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }
    public string DeclaredIntent { get; private set; } = null!;

    /// <summary>M2 seam: embedding of the declared intent (PostgreSQL <c>bytea</c>). Null in M1.</summary>
    public byte[]? IntentEmbedding { get; private set; }

    // EF Core materialization ctor.
    private FocusSession() { }

    public static OneOf<FocusSession, ValidationError> Create(
        DateTimeOffset startedAt,
        string declaredIntent,
        Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(declaredIntent))
            return ValidationError.Required(nameof(declaredIntent));

        return new FocusSession
        {
            Id = id ?? Guid.NewGuid(),
            StartedAt = startedAt,
            EndedAt = null,
            DeclaredIntent = declaredIntent.Trim(),
            IntentEmbedding = null
        };
    }

    /// <summary>Closes the session. Idempotent guard: a session may only be stopped once.</summary>
    public OneOf<FocusSession, ValidationError> Stop(DateTimeOffset endedAt)
    {
        if (EndedAt is not null)
            return new ValidationError("Focus session is already stopped.");
        if (endedAt < StartedAt)
            return new ValidationError("endedAt cannot precede startedAt.");

        EndedAt = endedAt;
        return this;
    }
}
