using CalendarManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CalendarManager.Infrastructure.Data.Configurations;

public class ConnectionAuditLogConfiguration : IEntityTypeConfiguration<ConnectionAuditLog>
{
    public void Configure(EntityTypeBuilder<ConnectionAuditLog> builder)
    {
        builder.Property(a => a.UserId)
            .IsRequired();

        builder.Property(a => a.AccountEmail)
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(a => a.UserId);
    }
}
