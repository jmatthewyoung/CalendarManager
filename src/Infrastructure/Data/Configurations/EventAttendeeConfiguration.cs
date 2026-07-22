using CalendarManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CalendarManager.Infrastructure.Data.Configurations;

public class EventAttendeeConfiguration : IEntityTypeConfiguration<EventAttendee>
{
    public void Configure(EntityTypeBuilder<EventAttendee> builder)
    {
        builder.Property(a => a.Email)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(a => a.Name)
            .HasMaxLength(512);

        builder.HasOne(a => a.CalendarEvent)
            .WithMany(e => e.Attendees)
            .HasForeignKey(a => a.CalendarEventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
