using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EnhancedBlocker.Domain;

namespace EnhancedBlocker.Infrastructure.Persistence.Configurations;

public sealed class CategoryDomainConfiguration : IEntityTypeConfiguration<CategoryDomain>
{
    public void Configure(EntityTypeBuilder<CategoryDomain> builder)
    {
        builder.ToTable("category_domains");
        builder.HasKey(c => c.Domain);

        builder.Property(c => c.Domain).IsRequired();
        builder.Property(c => c.Category).IsRequired();
        builder.Property(c => c.Confidence);
        builder.Property(c => c.AddedAt);
    }
}
