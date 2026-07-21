using CalendarManager.Domain.Entities;
using CalendarManager.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CalendarManager.Infrastructure.Data.Configurations;

public class CalendarConnectionConfiguration : IEntityTypeConfiguration<CalendarConnection>
{
    public void Configure(EntityTypeBuilder<CalendarConnection> builder)
    {
        builder.Property(c => c.UserId)
            .IsRequired();

        builder.Property(c => c.AccountEmail)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(c => c.EncryptedRefreshToken)
            .IsRequired();

        builder.Property(c => c.Colour)
            .HasConversion(c => c.Code, code => Colour.From(code))
            .HasMaxLength(7)
            .IsRequired();

        builder.HasMany(c => c.Events)
            .WithOne(e => e.Connection)
            .HasForeignKey(e => e.CalendarConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
