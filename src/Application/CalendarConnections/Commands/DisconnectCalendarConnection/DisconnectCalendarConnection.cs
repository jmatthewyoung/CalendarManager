using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Application.Common.Security;

namespace CalendarManager.Application.CalendarConnections.Commands.DisconnectCalendarConnection;

[Authorize]
public record DisconnectCalendarConnectionCommand(int Id) : IRequest;

public class DisconnectCalendarConnectionCommandHandler : IRequestHandler<DisconnectCalendarConnectionCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public DisconnectCalendarConnectionCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task Handle(DisconnectCalendarConnectionCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.CalendarConnections
            .Where(c => c.Id == request.Id && c.UserId == _user.Id)
            .SingleOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        _context.CalendarConnections.Remove(entity);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
