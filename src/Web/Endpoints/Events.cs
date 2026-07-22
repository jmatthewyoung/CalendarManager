using CalendarManager.Application.Events.Queries.GetMergedEvents;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CalendarManager.Web.Endpoints;

public class Events : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();

        groupBuilder.MapGet(GetMergedEvents);
    }

    [EndpointSummary("Get merged calendar events")]
    [EndpointDescription("Retrieves events from all visible connected calendars, plus local events, within the given date range.")]
    public static async Task<Ok<List<CalendarEventDto>>> GetMergedEvents(ISender sender, DateTimeOffset start, DateTimeOffset end)
    {
        var events = await sender.Send(new GetMergedEventsQuery(start, end));

        return TypedResults.Ok(events);
    }
}
