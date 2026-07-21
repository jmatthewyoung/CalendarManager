using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Application.Common.Security;

namespace CalendarManager.Application.Sync.Queries.GetSyncLogs;

[Authorize]
public record GetSyncLogsQuery : IRequest<List<SyncLogDto>>;

public class GetSyncLogsQueryHandler : IRequestHandler<GetSyncLogsQuery, List<SyncLogDto>>
{
    private const int MaxResults = 100;

    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly IUser _user;

    public GetSyncLogsQueryHandler(IApplicationDbContext context, IMapper mapper, IUser user)
    {
        _context = context;
        _mapper = mapper;
        _user = user;
    }

    public async Task<List<SyncLogDto>> Handle(GetSyncLogsQuery request, CancellationToken cancellationToken)
    {
        return await _context.SyncLogs
            .AsNoTracking()
            .Where(s => s.Connection.UserId == _user.Id)
            .OrderByDescending(s => s.RanAtUtc)
            .Take(MaxResults)
            .ProjectTo<SyncLogDto>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }
}
