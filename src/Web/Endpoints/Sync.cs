using CalendarManager.Application.Sync.Queries.GetSyncLogs;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CalendarManager.Web.Endpoints;

public class Sync : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();

        groupBuilder.MapGet(GetSyncLogs, "logs");
    }

    [EndpointSummary("Get recent sync history")]
    [EndpointDescription("Retrieves the most recent sync log entries across all of the current user's calendar connections.")]
    public static async Task<Ok<List<SyncLogDto>>> GetSyncLogs(ISender sender)
    {
        var logs = await sender.Send(new GetSyncLogsQuery());

        return TypedResults.Ok(logs);
    }
}
