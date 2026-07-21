using CalendarManager.Application.UserSettings.Commands.UpdateUserSettings;
using CalendarManager.Application.UserSettings.Queries.GetUserSettings;
using CalendarManager.Infrastructure.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CalendarManager.Web.Endpoints;

public class Users : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapIdentityApi<ApplicationUser>();

        groupBuilder.MapPost(Logout, "logout").RequireAuthorization();
        groupBuilder.MapGet(GetUserSettings, "settings").RequireAuthorization();
        groupBuilder.MapPut(UpdateUserSettings, "settings").RequireAuthorization();
    }

    [EndpointSummary("Log out")]
    [EndpointDescription("Logs out the current user by clearing the authentication cookie.")]
    public static async Task<Results<Ok, UnauthorizedHttpResult>> Logout(SignInManager<ApplicationUser> signInManager, [FromBody] object empty)
    {
        if (empty != null)
        {
            await signInManager.SignOutAsync();
            return TypedResults.Ok();
        }

        return TypedResults.Unauthorized();
    }

    [EndpointSummary("Get the current user's settings")]
    [EndpointDescription("Retrieves the current user's preferences, such as their time zone.")]
    public static async Task<Ok<UserSettingsDto>> GetUserSettings(ISender sender)
    {
        var settings = await sender.Send(new GetUserSettingsQuery());

        return TypedResults.Ok(settings);
    }

    [EndpointSummary("Update the current user's settings")]
    [EndpointDescription("Updates the current user's preferences, such as their time zone.")]
    public static async Task<NoContent> UpdateUserSettings(ISender sender, UpdateUserSettingsCommand command)
    {
        await sender.Send(command);

        return TypedResults.NoContent();
    }
}
