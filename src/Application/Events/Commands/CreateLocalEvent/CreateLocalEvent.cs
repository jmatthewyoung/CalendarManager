using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Application.Common.Security;
using CalendarManager.Domain.Entities;
using CalendarManager.Domain.ValueObjects;

namespace CalendarManager.Application.Events.Commands.CreateLocalEvent;

[Authorize]
public record CreateLocalEventCommand : IRequest<int>
{
    public string Title { get; init; } = null!;

    public DateTimeOffset StartUtc { get; init; }

    public DateTimeOffset EndUtc { get; init; }

    public bool IsAllDay { get; init; }

    public string Colour { get; init; } = null!;
}

public class CreateLocalEventCommandHandler : IRequestHandler<CreateLocalEventCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public CreateLocalEventCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<int> Handle(CreateLocalEventCommand request, CancellationToken cancellationToken)
    {
        var entity = new CalendarEvent
        {
            UserId = _user.Id!,
            IsLocal = true,
            Title = request.Title,
            StartUtc = request.StartUtc,
            EndUtc = request.EndUtc,
            IsAllDay = request.IsAllDay,
            ColourOverride = Colour.From(request.Colour)
        };

        _context.CalendarEvents.Add(entity);

        await _context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
