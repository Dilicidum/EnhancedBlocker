using OneOf;
using EnhancedBlocker.Application.Ports;
using EnhancedBlocker.Domain;

namespace EnhancedBlocker.Application.Categories;

/// <summary>Creates a category. Names are unique (case-insensitive).</summary>
public sealed record AddCategoryCommand(string Name);

public sealed class AddCategoryCommandHandler(ICategoryRepository repository)
{
    public async Task<OneOf<Category, ValidationError>> Handle(AddCategoryCommand request, CancellationToken ct)
    {
        var created = Category.Create(request.Name);
        if (created.IsT1)
            return created.AsT1;

        var category = created.AsT0;
        if (await repository.GetByNameAsync(category.Name, ct) is not null)
            return new ValidationError($"A category named '{category.Name}' already exists.");

        await repository.AddAsync(category, ct);
        return category;
    }
}
