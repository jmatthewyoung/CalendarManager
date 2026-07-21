namespace CalendarManager.Domain.Entities;

public class CalendarEvent : BaseAuditableEntity
{
    public int? CalendarConnectionId { get; set; }

    public string UserId { get; set; } = null!;

    public string? ExternalEventId { get; set; }

    public string Title { get; set; } = null!;

    public DateTimeOffset StartUtc { get; set; }

    public DateTimeOffset EndUtc { get; set; }

    public bool IsAllDay { get; set; }

    public bool IsLocal { get; set; }

    public Colour? ColourOverride { get; set; }

    public DateTimeOffset? ReminderSentAtUtc { get; set; }

    public CalendarConnection? Connection { get; set; }
}
