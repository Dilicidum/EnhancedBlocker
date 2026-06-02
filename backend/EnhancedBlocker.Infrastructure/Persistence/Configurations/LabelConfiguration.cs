using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EnhancedBlocker.Domain;

namespace EnhancedBlocker.Infrastructure.Persistence.Configurations;

public sealed class LabelConfiguration : IEntityTypeConfiguration<Label>
{
    public void Configure(EntityTypeBuilder<Label> builder)
    {
        builder.ToTable("labels");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Ts);
        builder.Property(l => l.Url).IsRequired();
        builder.Property(l => l.Title);
        builder.Property(l => l.Decision).HasConversion<string>().IsRequired();
        builder.Property(l => l.Source).HasConversion<string>().IsRequired();

        // M2 seam: Tier-1 features stored as PostgreSQL jsonb, nullable (null in M1).
        builder.Property(l => l.FeaturesJson).HasColumnType("jsonb");

        builder.HasIndex(l => l.Ts);
    }
}
