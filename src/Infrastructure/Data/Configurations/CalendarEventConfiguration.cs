using CalendarManager.Domain.Entities;
using CalendarManager.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CalendarManager.Infrastructure.Data.Configurations;

public class CalendarEventConfiguration : IEntityTypeConfiguration<CalendarEvent>
{
    public void Configure(EntityTypeBuilder<CalendarEvent> builder)
    {
        builder.Property(e => e.UserId)
            .IsRequired();

        builder.Property(e => e.ExternalEventId)
            .HasMaxLength(512);

        builder.Property(e => e.Title)
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(e => e.ColourOverride)
            .HasConversion(
                c => ReferenceEquals(c, null) ? null : c.Code,
                code => ReferenceEquals(code, null) ? null : Colour.From(code))
            .HasMaxLength(7);

        // Only synced (non-local) events need dedup against the provider's event id; local
        // events always have a null CalendarConnectionId/ExternalEventId, and SQL Server's
        // unique index treats repeated NULLs as duplicates, so those rows must be excluded.
        builder.HasIndex(e => new { e.CalendarConnectionId, e.ExternalEventId })
            .IsUnique()
            .HasFilter("[CalendarConnectionId] IS NOT NULL");
    }
}
