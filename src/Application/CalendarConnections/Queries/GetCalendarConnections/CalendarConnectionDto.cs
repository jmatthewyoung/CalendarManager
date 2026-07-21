using CalendarManager.Domain.Entities;
using CalendarManager.Domain.Enums;

namespace CalendarManager.Application.CalendarConnections.Queries.GetCalendarConnections;

public class CalendarConnectionDto
{
    public int Id { get; init; }

    public CalendarProvider Provider { get; init; }

    public string AccountEmail { get; init; } = null!;

    public string? Colour { get; init; }

    public bool IsVisible { get; init; }

    public bool NeedsReauth { get; init; }

    public DateTimeOffset? LastSyncedAtUtc { get; init; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<CalendarConnection, CalendarConnectionDto>();
        }
    }
}
