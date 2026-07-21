namespace CalendarManager.Domain.Entities;

/// <summary>
/// Records that a calendar was connected or disconnected. Provider/account details are
/// denormalized (not a foreign key to <see cref="CalendarConnection"/>) so the entry survives
/// the connection being deleted on disconnect.
/// </summary>
public class ConnectionAuditLog : BaseAuditableEntity
{
    public string UserId { get; set; } = null!;

    public CalendarProvider Provider { get; set; }

    public string AccountEmail { get; set; } = null!;

    public ConnectionAuditAction Action { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }
}
