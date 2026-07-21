namespace CalendarManager.Domain.Entities;

public class SyncLog : BaseAuditableEntity
{
    public int CalendarConnectionId { get; set; }

    public SyncStatus Status { get; set; }

    public string? Message { get; set; }

    public int EventsAdded { get; set; }

    public int EventsUpdated { get; set; }

    public int EventsRemoved { get; set; }

    public DateTimeOffset RanAtUtc { get; set; }

    public CalendarConnection Connection { get; set; } = null!;
}
