using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Application.Common.Security;
using CalendarManager.Domain.Enums;

namespace CalendarManager.Application.Events.Queries.GetMergedEvents;

[Authorize]
public record GetMergedEventsQuery(DateTimeOffset Start, DateTimeOffset End) : IRequest<List<CalendarEventDto>>;

public class GetMergedEventsQueryHandler : IRequestHandler<GetMergedEventsQuery, List<CalendarEventDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public GetMergedEventsQueryHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<List<CalendarEventDto>> Handle(GetMergedEventsQuery request, CancellationToken cancellationToken)
    {
        var events = await _context.CalendarEvents
            .AsNoTracking()
            .Include(e => e.Connection)
            .Where(e => e.UserId == _user.Id
                     && (e.IsLocal || e.Connection!.IsVisible)
                     && e.StartUtc < request.End
                     && e.EndUtc > request.Start)
            .ToListAsync(cancellationToken);

        return events.Select(e => new CalendarEventDto
        {
            Id = e.Id,
            CalendarConnectionId = e.CalendarConnectionId,
            Title = e.Title,
            StartUtc = e.StartUtc,
            EndUtc = e.EndUtc,
            IsAllDay = e.IsAllDay,
            IsLocal = e.IsLocal,
            Colour = e.ColourOverride?.Code ?? e.Connection?.Colour.Code,
            Provider = e.IsLocal ? CalendarProvider.Local : e.Connection!.Provider
        }).ToList();
    }
}
