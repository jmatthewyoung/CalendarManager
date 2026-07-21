using CalendarManager.Domain.Entities;

namespace CalendarManager.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<TodoList> TodoLists { get; }

    DbSet<TodoItem> TodoItems { get; }

    DbSet<CalendarConnection> CalendarConnections { get; }

    DbSet<CalendarEvent> CalendarEvents { get; }

    DbSet<SyncLog> SyncLogs { get; }

    DbSet<PushSubscription> PushSubscriptions { get; }

    DbSet<ConnectionAuditLog> ConnectionAuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
