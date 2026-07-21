using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Application.Common.Security;

namespace CalendarManager.Application.Events.Commands.DeleteLocalEvent;

[Authorize]
public record DeleteLocalEventCommand(int Id) : IRequest;

public class DeleteLocalEventCommandHandler : IRequestHandler<DeleteLocalEventCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public DeleteLocalEventCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task Handle(DeleteLocalEventCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.CalendarEvents
            .Where(e => e.Id == request.Id && e.UserId == _user.Id && e.IsLocal)
            .SingleOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        _context.CalendarEvents.Remove(entity);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
