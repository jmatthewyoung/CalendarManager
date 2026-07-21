using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Application.Common.Security;
using CalendarManager.Domain.Enums;

namespace CalendarManager.Application.CalendarConnections.Commands.BeginCalendarConnection;

[Authorize]
public record BeginCalendarConnectionCommand(CalendarProvider Provider, string RedirectUri) : IRequest<string>;

public class BeginCalendarConnectionCommandHandler : IRequestHandler<BeginCalendarConnectionCommand, string>
{
    private readonly IUser _user;
    private readonly IOAuthStateStore _stateStore;
    private readonly ICalendarProviderClientFactory _clientFactory;

    public BeginCalendarConnectionCommandHandler(IUser user, IOAuthStateStore stateStore, ICalendarProviderClientFactory clientFactory)
    {
        _user = user;
        _stateStore = stateStore;
        _clientFactory = clientFactory;
    }

    public Task<string> Handle(BeginCalendarConnectionCommand request, CancellationToken cancellationToken)
    {
        var state = _stateStore.Create(_user.Id!, request.Provider);
        var client = _clientFactory.Get(request.Provider);

        return Task.FromResult(client.BuildAuthorizationUrl(state, request.RedirectUri));
    }
}
