using CalendarManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CalendarManager.Infrastructure.Data.Configurations;

public class SyncLogConfiguration : IEntityTypeConfiguration<SyncLog>
{
    public void Configure(EntityTypeBuilder<SyncLog> builder)
    {
        builder.Property(s => s.Message)
            .HasMaxLength(2048);

        builder.HasOne(s => s.Connection)
            .WithMany()
            .HasForeignKey(s => s.CalendarConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
