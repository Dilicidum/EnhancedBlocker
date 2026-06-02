using EnhancedBlocker.Application.Messaging;
using EnhancedBlocker.Application.Ports;

namespace EnhancedBlocker.Application.Rules;

/// <summary>Deletes a Tier-0 rule by id. Result is <c>true</c> if a rule was removed.</summary>
public sealed record DeleteRuleCommand(Guid Id) : IRequest<bool>;

public sealed class DeleteRuleCommandHandler(IRuleRepository repository)
    : IRequestHandler<DeleteRuleCommand, bool>
{
    public Task<bool> Handle(DeleteRuleCommand request, CancellationToken ct) =>
        repository.DeleteAsync(request.Id, ct);
}
