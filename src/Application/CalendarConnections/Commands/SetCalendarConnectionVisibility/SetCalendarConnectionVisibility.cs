using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Application.Common.Security;

namespace CalendarManager.Application.CalendarConnections.Commands.SetCalendarConnectionVisibility;

[Authorize]
public record SetCalendarConnectionVisibilityCommand(int Id, bool IsVisible) : IRequest;

public class SetCalendarConnectionVisibilityCommandHandler : IRequestHandler<SetCalendarConnectionVisibilityCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public SetCalendarConnectionVisibilityCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task Handle(SetCalendarConnectionVisibilityCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.CalendarConnections
            .Where(c => c.Id == request.Id && c.UserId == _user.Id)
            .SingleOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        entity.IsVisible = request.IsVisible;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
