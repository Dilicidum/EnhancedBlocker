using EnhancedBlocker.Application.Ports;
using EnhancedBlocker.Domain;

namespace EnhancedBlocker.Application.Categories;

/// <summary>Lists all categories (alphabetical).</summary>
public sealed record ListCategoriesQuery;

public sealed class ListCategoriesQueryHandler(ICategoryRepository repository)
{
    public Task<IReadOnlyList<Category>> Handle(ListCategoriesQuery request, CancellationToken ct) =>
        repository.ListAsync(ct);
}
