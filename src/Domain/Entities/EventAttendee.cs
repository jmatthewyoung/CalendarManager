using CalendarManager.Domain.Common;
using CalendarManager.Domain.Enums;

namespace CalendarManager.Domain.Entities;

public class EventAttendee : BaseEntity
{
    public int CalendarEventId { get; set; }

    public string Email { get; set; } = null!;

    public string? Name { get; set; }

    public AttendeeResponseStatus ResponseStatus { get; set; }

    public CalendarEvent CalendarEvent { get; set; } = null!;
}
