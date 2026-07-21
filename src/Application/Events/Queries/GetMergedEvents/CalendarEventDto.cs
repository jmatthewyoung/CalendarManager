using CalendarManager.Domain.Enums;

namespace CalendarManager.Application.Events.Queries.GetMergedEvents;

public class CalendarEventDto
{
    public int Id { get; init; }

    public int? CalendarConnectionId { get; init; }

    public string Title { get; init; } = null!;

    public DateTimeOffset StartUtc { get; init; }

    public DateTimeOffset EndUtc { get; init; }

    public bool IsAllDay { get; init; }

    public bool IsLocal { get; init; }

    public string? Colour { get; init; }

    public CalendarProvider Provider { get; init; }
}
