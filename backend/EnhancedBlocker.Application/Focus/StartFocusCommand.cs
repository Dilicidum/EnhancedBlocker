using OneOf;
using EnhancedBlocker.Application.Ports;
using EnhancedBlocker.Domain;

namespace EnhancedBlocker.Application.Focus;

/// <summary>Starts a focus session with a declared intent. Returns the new session id.</summary>
public sealed record StartFocusCommand(string Intent, DateTimeOffset StartedAt);

public sealed class StartFocusCommandHandler(IFocusSessionRepository repository)
{
    public async Task<OneOf<Guid, ValidationError>> Handle(StartFocusCommand request, CancellationToken ct)
    {
        var created = FocusSession.Create(request.StartedAt, request.Intent);
        if (created.IsT1)
            return created.AsT1;

        var session = created.AsT0;
        await repository.AddAsync(session, ct);
        return session.Id;
    }
}
