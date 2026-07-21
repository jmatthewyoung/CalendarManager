using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Application.Common.Security;

namespace CalendarManager.Application.CalendarConnections.Queries.GetConnectionAuditLog;

[Authorize]
public record GetConnectionAuditLogQuery : IRequest<List<ConnectionAuditLogDto>>;

public class GetConnectionAuditLogQueryHandler : IRequestHandler<GetConnectionAuditLogQuery, List<ConnectionAuditLogDto>>
{
    private const int MaxResults = 100;

    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public GetConnectionAuditLogQueryHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<List<ConnectionAuditLogDto>> Handle(GetConnectionAuditLogQuery request, CancellationToken cancellationToken)
    {
        return await _context.ConnectionAuditLogs
            .AsNoTracking()
            .Where(a => a.UserId == _user.Id)
            .OrderByDescending(a => a.OccurredAtUtc)
            .Take(MaxResults)
            .Select(a => new ConnectionAuditLogDto
            {
                Id = a.Id,
                Provider = a.Provider,
                AccountEmail = a.AccountEmail,
                Action = a.Action,
                OccurredAtUtc = a.OccurredAtUtc
            })
            .ToListAsync(cancellationToken);
    }
}
