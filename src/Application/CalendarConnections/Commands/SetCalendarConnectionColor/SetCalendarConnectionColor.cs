using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Application.Common.Security;
using CalendarManager.Domain.ValueObjects;

namespace CalendarManager.Application.CalendarConnections.Commands.SetCalendarConnectionColor;

[Authorize]
public record SetCalendarConnectionColorCommand : IRequest
{
    public int Id { get; init; }

    public string? Colour { get; init; }
}

public class SetCalendarConnectionColorCommandHandler : IRequestHandler<SetCalendarConnectionColorCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public SetCalendarConnectionColorCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task Handle(SetCalendarConnectionColorCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.CalendarConnections
            .Where(c => c.Id == request.Id && c.UserId == _user.Id)
            .SingleOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        entity.Colour = Colour.From(request.Colour!);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
