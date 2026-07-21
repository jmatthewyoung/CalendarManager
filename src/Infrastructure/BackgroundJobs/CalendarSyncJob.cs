using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Application.Sync.Commands.SyncCalendarConnection;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace CalendarManager.Infrastructure.BackgroundJobs;

/// <summary>
/// Polls every active calendar connection on a fixed interval (see <see cref="QuartzConfig"/>).
/// Each connection is synced independently so one broken connection (revoked token, provider
/// outage) never blocks the rest of the tick.
/// </summary>
public class CalendarSyncJob : IJob
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;
    private readonly ILogger<CalendarSyncJob> _logger;

    public CalendarSyncJob(IApplicationDbContext context, ISender sender, ILogger<CalendarSyncJob> logger)
    {
        _context = context;
        _sender = sender;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var connectionIds = await _context.CalendarConnections
            .Where(c => !c.NeedsReauth)
            .Select(c => c.Id)
            .ToListAsync(context.CancellationToken);

        foreach (var connectionId in connectionIds)
        {
            try
            {
                await _sender.Send(new SyncCalendarConnectionCommand(connectionId), context.CancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Calendar sync job failed for connection {CalendarConnectionId}.", connectionId);
            }
        }
    }
}
