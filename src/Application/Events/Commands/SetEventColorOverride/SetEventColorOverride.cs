using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Application.Common.Security;
using CalendarManager.Domain.ValueObjects;

namespace CalendarManager.Application.Events.Commands.SetEventColorOverride;

[Authorize]
public record SetEventColorOverrideCommand : IRequest
{
    public int Id { get; init; }

    public string Colour { get; init; } = null!;
}

public class SetEventColorOverrideCommandHandler : IRequestHandler<SetEventColorOverrideCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public SetEventColorOverrideCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task Handle(SetEventColorOverrideCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.CalendarEvents
            .Where(e => e.Id == request.Id && e.UserId == _user.Id)
            .SingleOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        entity.ColourOverride = Colour.From(request.Colour);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
