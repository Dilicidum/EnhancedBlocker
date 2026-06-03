using OneOf;
using EnhancedBlocker.Application.Ports;
using EnhancedBlocker.Domain;

namespace EnhancedBlocker.Application.Events;

/// <summary>A single event to log (transport-shaped; the handler runs the domain factory).</summary>
public sealed record LogEventInput(
    DateTimeOffset Ts,
    string Url,
    string Domain,
    string? Title,
    int TabId,
    EventType Type,
    Guid? FocusSessionId,
    long? DurationMs);

/// <summary>Logs a batch of tracked events. Returns the number persisted.</summary>
public sealed record LogEventsCommand(IReadOnlyList<LogEventInput> Events);

public sealed class LogEventsCommandHandler(IEventStore store)
{
    public async Task<OneOf<int, ValidationError>> Handle(LogEventsCommand request, CancellationToken ct)
    {
        if (request.Events.Count == 0)
            return 0;

        var entities = new List<Event>(request.Events.Count);
        foreach (var input in request.Events)
        {
            var created = Event.Create(
                input.Ts, input.Url, input.Domain, input.Title,
                input.TabId, input.Type, input.FocusSessionId, input.DurationMs);

            if (created.IsT1)
                return created.AsT1;

            entities.Add(created.AsT0);
        }

        await store.AddRangeAsync(entities, ct);
        return entities.Count;
    }
}
