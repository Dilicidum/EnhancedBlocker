using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EnhancedBlocker.Domain;

namespace EnhancedBlocker.Infrastructure.Persistence.Configurations;

public sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("events");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Url).IsRequired();
        builder.Property(e => e.Domain).IsRequired();
        builder.Property(e => e.Title);
        builder.Property(e => e.TabId);
        builder.Property(e => e.Type).HasConversion<string>().IsRequired();
        builder.Property(e => e.FocusSessionId);
        builder.Property(e => e.DurationMs);
        builder.Property(e => e.Ts);

        builder.HasIndex(e => e.Ts);
        builder.HasIndex(e => e.Domain);
        builder.HasIndex(e => e.FocusSessionId);
    }
}
