using CalendarManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CalendarManager.Infrastructure.Data.Configurations;

public class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        builder.Property(p => p.UserId)
            .IsRequired();

        builder.Property(p => p.Endpoint)
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(p => p.P256dhKey)
            .IsRequired();

        builder.Property(p => p.AuthKey)
            .IsRequired();

        // No DB-level uniqueness on (UserId, Endpoint): the endpoint URL is far longer than
        // SQL Server's ~1700-byte composite index key limit. Dedup is handled in
        // RegisterPushSubscriptionCommandHandler instead.
        builder.HasIndex(p => p.UserId);
    }
}
