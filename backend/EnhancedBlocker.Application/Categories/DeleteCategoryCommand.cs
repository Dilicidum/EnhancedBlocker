using EnhancedBlocker.Application.Ports;

namespace EnhancedBlocker.Application.Categories;

/// <summary>Deletes a category by id. Existing rules keep their (now free-text) category value.</summary>
public sealed record DeleteCategoryCommand(Guid Id);

public sealed class DeleteCategoryCommandHandler(ICategoryRepository repository)
{
    public Task<bool> Handle(DeleteCategoryCommand request, CancellationToken ct) =>
        repository.DeleteAsync(request.Id, ct);
}
