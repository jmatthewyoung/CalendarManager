namespace CalendarManager.Domain.Entities;

public class CalendarEvent : BaseAuditableEntity
{
    public int CalendarConnectionId { get; set; }

    public string ExternalEventId { get; set; } = null!;

    public string Title { get; set; } = null!;

    public DateTimeOffset StartUtc { get; set; }

    public DateTimeOffset EndUtc { get; set; }

    public bool IsAllDay { get; set; }

    public CalendarConnection Connection { get; set; } = null!;
}
