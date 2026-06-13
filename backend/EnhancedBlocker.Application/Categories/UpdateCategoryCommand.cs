using OneOf;
using EnhancedBlocker.Application.Ports;
using EnhancedBlocker.Domain;

namespace EnhancedBlocker.Application.Categories;

/// <summary>Renames an existing category. Names stay unique (case-insensitive).</summary>
public sealed record UpdateCategoryCommand(Guid Id, string Name);

public sealed class UpdateCategoryCommandHandler(ICategoryRepository repository)
{
    public async Task<OneOf<Category, ValidationError>> Handle(UpdateCategoryCommand request, CancellationToken ct)
    {
        var category = await repository.GetAsync(request.Id, ct);
        if (category is null)
            return new ValidationError("Category not found.");

        var existing = await repository.GetByNameAsync(request.Name.Trim(), ct);
        if (existing is not null && existing.Id != request.Id)
            return new ValidationError($"A category named '{request.Name.Trim()}' already exists.");

        var updated = category.Update(request.Name);
        if (updated.IsT1)
            return updated.AsT1;

        await repository.UpdateAsync(category, ct);
        return category;
    }
}
