using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EnhancedBlocker.Domain;

namespace EnhancedBlocker.Infrastructure.Persistence.Configurations;

public sealed class RuleConfiguration : IEntityTypeConfiguration<Rule>
{
    public void Configure(EntityTypeBuilder<Rule> builder)
    {
        builder.ToTable("rules");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Pattern).IsRequired();
        builder.Property(r => r.Match).HasConversion<string>().IsRequired();
        builder.Property(r => r.Kind).HasConversion<string>().IsRequired();
        builder.Property(r => r.Source).HasConversion<string>().IsRequired();
        builder.Property(r => r.Category);

        builder.HasIndex(r => r.Pattern);
    }
}
