using CalendarManager.Domain.Entities;
using CalendarManager.Domain.Enums;

namespace CalendarManager.Application.Events.Queries.GetMergedEvents;

public class CalendarEventDto
{
    public int Id { get; init; }

    public int CalendarConnectionId { get; init; }

    public string Title { get; init; } = null!;

    public DateTimeOffset StartUtc { get; init; }

    public DateTimeOffset EndUtc { get; init; }

    public bool IsAllDay { get; init; }

    public string? Colour { get; init; }

    public CalendarProvider Provider { get; init; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<CalendarEvent, CalendarEventDto>()
                .ForMember(d => d.Colour, opt => opt.MapFrom(s => s.Connection.Colour))
                .ForMember(d => d.Provider, opt => opt.MapFrom(s => s.Connection.Provider));
        }
    }
}
