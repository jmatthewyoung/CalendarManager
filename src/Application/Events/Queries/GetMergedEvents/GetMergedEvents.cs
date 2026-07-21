using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Application.Common.Security;

namespace CalendarManager.Application.Events.Queries.GetMergedEvents;

[Authorize]
public record GetMergedEventsQuery(DateTimeOffset Start, DateTimeOffset End) : IRequest<List<CalendarEventDto>>;

public class GetMergedEventsQueryHandler : IRequestHandler<GetMergedEventsQuery, List<CalendarEventDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly IUser _user;

    public GetMergedEventsQueryHandler(IApplicationDbContext context, IMapper mapper, IUser user)
    {
        _context = context;
        _mapper = mapper;
        _user = user;
    }

    public async Task<List<CalendarEventDto>> Handle(GetMergedEventsQuery request, CancellationToken cancellationToken)
    {
        return await _context.CalendarEvents
            .AsNoTracking()
            .Where(e => e.Connection.UserId == _user.Id
                     && e.Connection.IsVisible
                     && e.StartUtc < request.End
                     && e.EndUtc > request.Start)
            .ProjectTo<CalendarEventDto>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }
}
