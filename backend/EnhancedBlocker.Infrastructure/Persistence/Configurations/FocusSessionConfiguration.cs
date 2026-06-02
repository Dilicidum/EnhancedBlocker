using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EnhancedBlocker.Domain;

namespace EnhancedBlocker.Infrastructure.Persistence.Configurations;

public sealed class FocusSessionConfiguration : IEntityTypeConfiguration<FocusSession>
{
    public void Configure(EntityTypeBuilder<FocusSession> builder)
    {
        builder.ToTable("focus_sessions");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.StartedAt);
        builder.Property(s => s.EndedAt);
        builder.Property(s => s.DeclaredIntent).IsRequired();

        // M2 seam: intent embedding stored as PostgreSQL bytea, nullable (null in M1).
        builder.Property(s => s.IntentEmbedding).HasColumnType("bytea");

        builder.HasIndex(s => s.StartedAt);
        builder.HasIndex(s => s.EndedAt);
    }
}
