using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Application.Common.Security;
using CalendarManager.Domain.ValueObjects;

namespace CalendarManager.Application.CalendarConnections.Queries.GetCalendarConnections;

[Authorize]
public record GetCalendarConnectionsQuery : IRequest<CalendarConnectionsVm>;

public class GetCalendarConnectionsQueryHandler : IRequestHandler<GetCalendarConnectionsQuery, CalendarConnectionsVm>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly IUser _user;

    public GetCalendarConnectionsQueryHandler(IApplicationDbContext context, IMapper mapper, IUser user)
    {
        _context = context;
        _mapper = mapper;
        _user = user;
    }

    public async Task<CalendarConnectionsVm> Handle(GetCalendarConnectionsQuery request, CancellationToken cancellationToken)
    {
        return new CalendarConnectionsVm
        {
            Colours =
            [
                new ColourDto { Code = Colour.Grey, Name = nameof(Colour.Grey) },
                new ColourDto { Code = Colour.Purple, Name = nameof(Colour.Purple) },
                new ColourDto { Code = Colour.Blue, Name = nameof(Colour.Blue) },
                new ColourDto { Code = Colour.Teal, Name = nameof(Colour.Teal) },
                new ColourDto { Code = Colour.Green, Name = nameof(Colour.Green) },
                new ColourDto { Code = Colour.Orange, Name = nameof(Colour.Orange) },
                new ColourDto { Code = Colour.Red, Name = nameof(Colour.Red) },
            ],

            Connections = await _context.CalendarConnections
                .AsNoTracking()
                .Where(c => c.UserId == _user.Id)
                .ProjectTo<CalendarConnectionDto>(_mapper.ConfigurationProvider)
                .OrderBy(c => c.AccountEmail)
                .ToListAsync(cancellationToken)
        };
    }
}
