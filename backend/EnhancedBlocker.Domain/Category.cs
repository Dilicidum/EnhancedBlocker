using OneOf;

namespace EnhancedBlocker.Domain;

/// <summary>
/// A user-managed category in the blocking vocabulary (e.g. "news", "social media",
/// "brainrot"). Categories are assignable to <see cref="Rule"/>s and are editable at
/// runtime via the API/settings page (so this is an entity, not a compile-time enum).
/// </summary>
public sealed class Category
{
    public const int MaxNameLength = 50;

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;

    // EF Core materialization ctor.
    private Category() { }

    public static OneOf<Category, ValidationError> Create(string name, Guid? id = null)
    {
        var validated = Validate(name);
        if (validated.IsT1)
            return validated.AsT1;

        return new Category { Id = id ?? Guid.NewGuid(), Name = validated.AsT0 };
    }

    public OneOf<Category, ValidationError> Update(string name)
    {
        var validated = Validate(name);
        if (validated.IsT1)
            return validated.AsT1;

        Name = validated.AsT0;
        return this;
    }

    private static OneOf<string, ValidationError> Validate(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ValidationError.Required(nameof(name));

        var normalized = name.Trim();
        if (normalized.Length > MaxNameLength)
            return new ValidationError($"name must be {MaxNameLength} characters or fewer.");

        return normalized;
    }
}
