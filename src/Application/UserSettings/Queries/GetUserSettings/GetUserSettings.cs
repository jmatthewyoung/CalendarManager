using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Application.Common.Security;

namespace CalendarManager.Application.UserSettings.Queries.GetUserSettings;

public record UserSettingsDto(string? TimeZoneId);

[Authorize]
public record GetUserSettingsQuery : IRequest<UserSettingsDto>;

public class GetUserSettingsQueryHandler : IRequestHandler<GetUserSettingsQuery, UserSettingsDto>
{
    private readonly IIdentityService _identityService;
    private readonly IUser _user;

    public GetUserSettingsQueryHandler(IIdentityService identityService, IUser user)
    {
        _identityService = identityService;
        _user = user;
    }

    public async Task<UserSettingsDto> Handle(GetUserSettingsQuery request, CancellationToken cancellationToken)
    {
        var timeZoneId = await _identityService.GetTimeZoneIdAsync(_user.Id!);

        return new UserSettingsDto(timeZoneId);
    }
}
