using OneOf;
using EnhancedBlocker.Application.Ports;
using EnhancedBlocker.Domain;

namespace EnhancedBlocker.Application.Focus;

/// <summary>
/// Stops a focus session. If <see cref="SessionId"/> is null, stops the currently active session.
/// Returns the stopped session id.
/// </summary>
public sealed record StopFocusCommand(Guid? SessionId, DateTimeOffset EndedAt);

public sealed class StopFocusCommandHandler(IFocusSessionRepository repository)
{
    public async Task<OneOf<Guid, ValidationError>> Handle(StopFocusCommand request, CancellationToken ct)
    {
        var session = request.SessionId is { } id
            ? await repository.GetAsync(id, ct)
            : await repository.GetActiveAsync(ct);

        if (session is null)
            return new ValidationError(
                request.SessionId is null
                    ? "No active focus session to stop."
                    : "Focus session not found.");

        var stopped = session.Stop(request.EndedAt);
        if (stopped.IsT1)
            return stopped.AsT1;

        await repository.UpdateAsync(session, ct);
        return session.Id;
    }
}
