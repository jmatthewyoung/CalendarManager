using CalendarManager.Application.PushSubscriptions.Commands.RegisterPushSubscription;
using CalendarManager.Application.PushSubscriptions.Commands.RemovePushSubscription;
using CalendarManager.Application.PushSubscriptions.Queries.GetPushPublicKey;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CalendarManager.Web.Endpoints;

public class Push : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();

        groupBuilder.MapGet(GetPushPublicKey, "public-key");
        groupBuilder.MapPost(Subscribe, "subscribe");
        groupBuilder.MapDelete(Unsubscribe, "subscribe");
    }

    [EndpointSummary("Get the VAPID public key")]
    [EndpointDescription("Returns the VAPID public key the client needs to create a push subscription.")]
    public static async Task<Ok<string>> GetPushPublicKey(ISender sender)
    {
        var key = await sender.Send(new GetPushPublicKeyQuery());

        return TypedResults.Ok(key);
    }

    [EndpointSummary("Register a Web Push subscription")]
    [EndpointDescription("Registers (or updates) a browser push subscription for the current user.")]
    public static async Task<NoContent> Subscribe(ISender sender, RegisterPushSubscriptionCommand command)
    {
        await sender.Send(command);

        return TypedResults.NoContent();
    }

    [EndpointSummary("Remove a Web Push subscription")]
    [EndpointDescription("Removes a browser push subscription for the current user.")]
    public static async Task<NoContent> Unsubscribe(ISender sender, string endpoint)
    {
        await sender.Send(new RemovePushSubscriptionCommand(endpoint));

        return TypedResults.NoContent();
    }
}
