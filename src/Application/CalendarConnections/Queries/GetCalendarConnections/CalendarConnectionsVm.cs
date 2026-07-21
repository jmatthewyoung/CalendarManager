namespace CalendarManager.Application.CalendarConnections.Queries.GetCalendarConnections;

public class CalendarConnectionsVm
{
    public IReadOnlyCollection<CalendarConnectionDto> Connections { get; init; } = [];

    public IReadOnlyCollection<ColourDto> Colours { get; init; } = [];
}

public class ColourDto
{
    public string? Code { get; init; }

    public string? Name { get; init; }
}
