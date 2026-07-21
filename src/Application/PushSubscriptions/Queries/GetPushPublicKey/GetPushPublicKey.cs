using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Application.Common.Security;

namespace CalendarManager.Application.PushSubscriptions.Queries.GetPushPublicKey;

[Authorize]
public record GetPushPublicKeyQuery : IRequest<string>;

public class GetPushPublicKeyQueryHandler : IRequestHandler<GetPushPublicKeyQuery, string>
{
    private readonly IPushNotificationService _pushNotificationService;

    public GetPushPublicKeyQueryHandler(IPushNotificationService pushNotificationService)
    {
        _pushNotificationService = pushNotificationService;
    }

    public Task<string> Handle(GetPushPublicKeyQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_pushNotificationService.PublicKey);
    }
}
