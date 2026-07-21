using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Application.Common.Security;

namespace CalendarManager.Application.UserSettings.Commands.UpdateUserSettings;

[Authorize]
public record UpdateUserSettingsCommand(string TimeZoneId) : IRequest;

public class UpdateUserSettingsCommandHandler : IRequestHandler<UpdateUserSettingsCommand>
{
    private readonly IIdentityService _identityService;
    private readonly IUser _user;

    public UpdateUserSettingsCommandHandler(IIdentityService identityService, IUser user)
    {
        _identityService = identityService;
        _user = user;
    }

    public async Task Handle(UpdateUserSettingsCommand request, CancellationToken cancellationToken)
    {
        var result = await _identityService.SetTimeZoneIdAsync(_user.Id!, request.TimeZoneId);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(' ', result.Errors));
        }
    }
}
