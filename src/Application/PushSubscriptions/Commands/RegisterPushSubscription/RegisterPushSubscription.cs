using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Application.Common.Security;
using CalendarManager.Domain.Entities;

namespace CalendarManager.Application.PushSubscriptions.Commands.RegisterPushSubscription;

[Authorize]
public record RegisterPushSubscriptionCommand : IRequest
{
    public string Endpoint { get; init; } = null!;

    public string P256dhKey { get; init; } = null!;

    public string AuthKey { get; init; } = null!;
}

public class RegisterPushSubscriptionCommandHandler : IRequestHandler<RegisterPushSubscriptionCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public RegisterPushSubscriptionCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task Handle(RegisterPushSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var existing = await _context.PushSubscriptions
            .Where(p => p.UserId == _user.Id && p.Endpoint == request.Endpoint)
            .SingleOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            existing.P256dhKey = request.P256dhKey;
            existing.AuthKey = request.AuthKey;
        }
        else
        {
            _context.PushSubscriptions.Add(new PushSubscription
            {
                UserId = _user.Id!,
                Endpoint = request.Endpoint,
                P256dhKey = request.P256dhKey,
                AuthKey = request.AuthKey
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
