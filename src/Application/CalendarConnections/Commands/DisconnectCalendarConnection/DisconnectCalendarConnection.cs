using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Application.Common.Security;
using CalendarManager.Domain.Entities;
using CalendarManager.Domain.Enums;

namespace CalendarManager.Application.CalendarConnections.Commands.DisconnectCalendarConnection;

[Authorize]
public record DisconnectCalendarConnectionCommand(int Id) : IRequest;

public class DisconnectCalendarConnectionCommandHandler : IRequestHandler<DisconnectCalendarConnectionCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly TimeProvider _timeProvider;

    public DisconnectCalendarConnectionCommandHandler(IApplicationDbContext context, IUser user, TimeProvider timeProvider)
    {
        _context = context;
        _user = user;
        _timeProvider = timeProvider;
    }

    public async Task Handle(DisconnectCalendarConnectionCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.CalendarConnections
            .Where(c => c.Id == request.Id && c.UserId == _user.Id)
            .SingleOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        _context.CalendarConnections.Remove(entity);

        _context.ConnectionAuditLogs.Add(new ConnectionAuditLog
        {
            UserId = _user.Id!,
            Provider = entity.Provider,
            AccountEmail = entity.AccountEmail,
            Action = ConnectionAuditAction.Disconnected,
            OccurredAtUtc = _timeProvider.GetUtcNow()
        });

        await _context.SaveChangesAsync(cancellationToken);
    }
}
