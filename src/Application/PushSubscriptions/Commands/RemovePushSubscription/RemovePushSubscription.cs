using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Application.Common.Security;

namespace CalendarManager.Application.PushSubscriptions.Commands.RemovePushSubscription;

[Authorize]
public record RemovePushSubscriptionCommand(string Endpoint) : IRequest;

public class RemovePushSubscriptionCommandHandler : IRequestHandler<RemovePushSubscriptionCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public RemovePushSubscriptionCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task Handle(RemovePushSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var existing = await _context.PushSubscriptions
            .Where(p => p.UserId == _user.Id && p.Endpoint == request.Endpoint)
            .SingleOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            _context.PushSubscriptions.Remove(existing);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
