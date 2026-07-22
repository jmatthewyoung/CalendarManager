using CalendarManager.Application.Events.Commands.DeleteLocalEvent;
using CalendarManager.Application.Events.Commands.SetEventColorOverride;
using CalendarManager.Application.Events.Commands.UpdateLocalEvent;
using CalendarManager.Application.Events.Queries.GetMergedEvents;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CalendarManager.Web.Endpoints;

public class Events : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();

        groupBuilder.MapGet(GetMergedEvents);
        groupBuilder.MapPut(UpdateLocalEvent, "{id}");
        groupBuilder.MapDelete(DeleteLocalEvent, "{id}");
        groupBuilder.MapPatch(SetEventColorOverride, "{id}/color");
    }

    [EndpointSummary("Get merged calendar events")]
    [EndpointDescription("Retrieves events from all visible connected calendars, plus local events, within the given date range.")]
    public static async Task<Ok<List<CalendarEventDto>>> GetMergedEvents(ISender sender, DateTimeOffset start, DateTimeOffset end)
    {
        var events = await sender.Send(new GetMergedEventsQuery(start, end));

        return TypedResults.Ok(events);
    }

    [EndpointSummary("Update a local event")]
    [EndpointDescription("Updates a local event's details. The ID in the URL must match the ID in the payload. Synced events cannot be edited.")]
    public static async Task<Results<NoContent, BadRequest>> UpdateLocalEvent(ISender sender, int id, UpdateLocalEventCommand command)
    {
        if (id != command.Id) return TypedResults.BadRequest();

        await sender.Send(command);

        return TypedResults.NoContent();
    }

    [EndpointSummary("Delete a local event")]
    [EndpointDescription("Deletes a local event. Synced events cannot be deleted this way — disconnect the calendar instead.")]
    public static async Task<NoContent> DeleteLocalEvent(ISender sender, int id)
    {
        await sender.Send(new DeleteLocalEventCommand(id));

        return TypedResults.NoContent();
    }

    [EndpointSummary("Set an event's colour override")]
    [EndpointDescription("Sets a per-event colour override, taking precedence over the source calendar's colour. The ID in the URL must match the ID in the payload.")]
    public static async Task<Results<NoContent, BadRequest>> SetEventColorOverride(ISender sender, int id, SetEventColorOverrideCommand command)
    {
        if (id != command.Id) return TypedResults.BadRequest();

        await sender.Send(command);

        return TypedResults.NoContent();
    }
}
