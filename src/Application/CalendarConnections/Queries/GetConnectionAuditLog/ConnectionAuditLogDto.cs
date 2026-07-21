using CalendarManager.Domain.Enums;

namespace CalendarManager.Application.CalendarConnections.Queries.GetConnectionAuditLog;

public class ConnectionAuditLogDto
{
    public int Id { get; init; }

    public CalendarProvider Provider { get; init; }

    public string AccountEmail { get; init; } = null!;

    public ConnectionAuditAction Action { get; init; }

    public DateTimeOffset OccurredAtUtc { get; init; }
}
