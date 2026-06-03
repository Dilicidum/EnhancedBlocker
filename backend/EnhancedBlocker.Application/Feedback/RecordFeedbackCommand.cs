using OneOf;
using EnhancedBlocker.Application.Ports;
using EnhancedBlocker.Domain;

namespace EnhancedBlocker.Application.Feedback;

/// <summary>Records a feedback <see cref="Label"/> (e.g. Good call / Bad call). Returns its id.</summary>
public sealed record RecordFeedbackCommand(
    string Url,
    string? Title,
    Decision Decision,
    LabelSource Source,
    DateTimeOffset Ts);

public sealed class RecordFeedbackCommandHandler(ILabelStore store)
{
    public async Task<OneOf<Guid, ValidationError>> Handle(RecordFeedbackCommand request, CancellationToken ct)
    {
        var created = Label.Create(
            request.Ts, request.Url, request.Title, request.Decision, request.Source);

        if (created.IsT1)
            return created.AsT1;

        var label = created.AsT0;
        await store.AddAsync(label, ct);
        return label.Id;
    }
}
