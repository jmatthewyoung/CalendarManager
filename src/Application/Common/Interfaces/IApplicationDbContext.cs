using CalendarManager.Domain.Entities;

namespace CalendarManager.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<CalendarConnection> CalendarConnections { get; }

    DbSet<CalendarEvent> CalendarEvents { get; }

    DbSet<EventAttendee> EventAttendees { get; }

    DbSet<SyncLog> SyncLogs { get; }

    DbSet<PushSubscription> PushSubscriptions { get; }

    DbSet<ConnectionAuditLog> ConnectionAuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
