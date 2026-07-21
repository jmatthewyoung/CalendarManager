using CalendarManager.Application.CalendarConnections.Commands.BeginCalendarConnection;
using CalendarManager.Application.CalendarConnections.Commands.CompleteCalendarConnection;
using CalendarManager.Application.CalendarConnections.Commands.DisconnectCalendarConnection;
using CalendarManager.Application.CalendarConnections.Commands.SetCalendarConnectionColor;
using CalendarManager.Application.CalendarConnections.Commands.SetCalendarConnectionVisibility;
using CalendarManager.Application.CalendarConnections.Queries.GetCalendarConnections;
using CalendarManager.Application.CalendarConnections.Queries.GetConnectionAuditLog;
using CalendarManager.Application.Sync.Commands.SyncCalendarConnection;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CalendarManager.Web.Endpoints;

public class CalendarConnections : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();

        groupBuilder.MapGet(GetCalendarConnections);
        groupBuilder.MapPost(BeginCalendarConnection, "begin");
        groupBuilder.MapPost(CompleteCalendarConnection, "complete");
        groupBuilder.MapDelete(DisconnectCalendarConnection, "{id}");
        groupBuilder.MapPatch(SetCalendarConnectionColor, "{id}/color");
        groupBuilder.MapPatch(SetCalendarConnectionVisibility, "{id}/visibility");
        groupBuilder.MapPost(ResyncCalendarConnection, "{id}/resync");
        groupBuilder.MapGet(GetConnectionAuditLog, "audit-log");
    }

    [EndpointSummary("Get connected calendars")]
    [EndpointDescription("Retrieves the current user's connected calendars and the supported colour palette.")]
    public static async Task<Ok<CalendarConnectionsVm>> GetCalendarConnections(ISender sender)
    {
        var vm = await sender.Send(new GetCalendarConnectionsQuery());

        return TypedResults.Ok(vm);
    }

    [EndpointSummary("Begin a calendar connection")]
    [EndpointDescription("Starts the OAuth flow for the given provider and returns the authorization URL to redirect the user to.")]
    public static async Task<Ok<string>> BeginCalendarConnection(ISender sender, BeginCalendarConnectionCommand command)
    {
        var url = await sender.Send(command);

        return TypedResults.Ok(url);
    }

    [EndpointSummary("Complete a calendar connection")]
    [EndpointDescription("Exchanges the OAuth authorization code for tokens and stores the new calendar connection.")]
    public static async Task<NoContent> CompleteCalendarConnection(ISender sender, CompleteCalendarConnectionCommand command)
    {
        await sender.Send(command);

        return TypedResults.NoContent();
    }

    [EndpointSummary("Disconnect a calendar")]
    [EndpointDescription("Removes a calendar connection and its cached events.")]
    public static async Task<NoContent> DisconnectCalendarConnection(ISender sender, int id)
    {
        await sender.Send(new DisconnectCalendarConnectionCommand(id));

        return TypedResults.NoContent();
    }

    [EndpointSummary("Set a calendar connection's colour")]
    [EndpointDescription("Updates the display colour for a connected calendar. The ID in the URL must match the ID in the payload.")]
    public static async Task<Results<NoContent, BadRequest>> SetCalendarConnectionColor(ISender sender, int id, SetCalendarConnectionColorCommand command)
    {
        if (id != command.Id) return TypedResults.BadRequest();

        await sender.Send(command);

        return TypedResults.NoContent();
    }

    [EndpointSummary("Set a calendar connection's visibility")]
    [EndpointDescription("Toggles whether a connected calendar's events appear in the merged view. The ID in the URL must match the ID in the payload.")]
    public static async Task<Results<NoContent, BadRequest>> SetCalendarConnectionVisibility(ISender sender, int id, SetCalendarConnectionVisibilityCommand command)
    {
        if (id != command.Id) return TypedResults.BadRequest();

        await sender.Send(command);

        return TypedResults.NoContent();
    }

    [EndpointSummary("Manually resync a calendar")]
    [EndpointDescription("Triggers an immediate sync of the calendar connection instead of waiting for the next scheduled run.")]
    public static async Task<NoContent> ResyncCalendarConnection(ISender sender, int id)
    {
        await sender.Send(new SyncCalendarConnectionCommand(id));

        return TypedResults.NoContent();
    }

    [EndpointSummary("Get the connection audit log")]
    [EndpointDescription("Retrieves the most recent connect/disconnect events for the current user's calendars.")]
    public static async Task<Ok<List<ConnectionAuditLogDto>>> GetConnectionAuditLog(ISender sender)
    {
        var log = await sender.Send(new GetConnectionAuditLogQuery());

        return TypedResults.Ok(log);
    }
}
