using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Application.Common.Security;
using CalendarManager.Domain.ValueObjects;

namespace CalendarManager.Application.Events.Commands.UpdateLocalEvent;

[Authorize]
public record UpdateLocalEventCommand : IRequest
{
    public int Id { get; init; }

    public string Title { get; init; } = null!;

    public DateTimeOffset StartUtc { get; init; }

    public DateTimeOffset EndUtc { get; init; }

    public bool IsAllDay { get; init; }

    public string Colour { get; init; } = null!;
}

public class UpdateLocalEventCommandHandler : IRequestHandler<UpdateLocalEventCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public UpdateLocalEventCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task Handle(UpdateLocalEventCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.CalendarEvents
            .Where(e => e.Id == request.Id && e.UserId == _user.Id && e.IsLocal)
            .SingleOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        entity.Title = request.Title;
        entity.StartUtc = request.StartUtc;
        entity.EndUtc = request.EndUtc;
        entity.IsAllDay = request.IsAllDay;
        entity.ColourOverride = Colour.From(request.Colour);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
