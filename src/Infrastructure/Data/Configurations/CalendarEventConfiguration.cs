using CalendarManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CalendarManager.Infrastructure.Data.Configurations;

public class CalendarEventConfiguration : IEntityTypeConfiguration<CalendarEvent>
{
    public void Configure(EntityTypeBuilder<CalendarEvent> builder)
    {
        builder.Property(e => e.ExternalEventId)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(e => e.Title)
            .HasMaxLength(1024)
            .IsRequired();

        builder.HasIndex(e => new { e.CalendarConnectionId, e.ExternalEventId })
            .IsUnique();
    }
}
