using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EnhancedBlocker.Domain;

namespace EnhancedBlocker.Infrastructure.Persistence.Configurations;

public sealed class DecisionLogConfiguration : IEntityTypeConfiguration<DecisionLog>
{
    public void Configure(EntityTypeBuilder<DecisionLog> builder)
    {
        builder.ToTable("decision_logs");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Ts);
        builder.Property(d => d.Url).IsRequired();
        builder.Property(d => d.Tier).IsRequired();
        builder.Property(d => d.Outcome).IsRequired();
        builder.Property(d => d.Score);

        builder.HasIndex(d => d.Ts);
    }
}
