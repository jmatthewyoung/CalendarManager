using CalendarManager.Application.Common.Interfaces;

namespace CalendarManager.Application.Reminders.Commands.DispatchEventReminders;

/// <summary>
/// Sends a push reminder for every event starting within <see cref="LeadTime"/> that hasn't
/// already had one dispatched. Invoked by <c>ReminderDispatchJob</c> every 5 minutes.
/// </summary>
public record DispatchEventRemindersCommand : IRequest;

public class DispatchEventRemindersCommandHandler : IRequestHandler<DispatchEventRemindersCommand>
{
    private static readonly TimeSpan LeadTime = TimeSpan.FromMinutes(10);

    private readonly IApplicationDbContext _context;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IIdentityService _identityService;
    private readonly TimeProvider _timeProvider;

    public DispatchEventRemindersCommandHandler(
        IApplicationDbContext context,
        IPushNotificationService pushNotificationService,
        IIdentityService identityService,
        TimeProvider timeProvider)
    {
        _context = context;
        _pushNotificationService = pushNotificationService;
        _identityService = identityService;
        _timeProvider = timeProvider;
    }

    public async Task Handle(DispatchEventRemindersCommand request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var horizon = now + LeadTime;

        var dueEvents = await _context.CalendarEvents
            .Where(e => e.ReminderSentAtUtc == null && e.StartUtc > now && e.StartUtc <= horizon)
            .ToListAsync(cancellationToken);

        if (dueEvents.Count == 0)
        {
            return;
        }

        var userIds = dueEvents.Select(e => e.UserId).Distinct().ToList();

        var subscriptionsByUser = (await _context.PushSubscriptions
            .Where(p => userIds.Contains(p.UserId))
            .ToListAsync(cancellationToken))
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var timeZoneCache = new Dictionary<string, TimeZoneInfo>();

        foreach (var calendarEvent in dueEvents)
        {
            calendarEvent.ReminderSentAtUtc = now;

            if (!subscriptionsByUser.TryGetValue(calendarEvent.UserId, out var subscriptions))
            {
                continue;
            }

            var timeZone = await GetTimeZoneAsync(calendarEvent.UserId, timeZoneCache);
            var localStart = TimeZoneInfo.ConvertTimeFromUtc(calendarEvent.StartUtc.UtcDateTime, timeZone);
            var zoneLabel = timeZone == TimeZoneInfo.Utc ? "UTC" : timeZone.Id;
            var body = $"Starts at {localStart:t} {zoneLabel}";

            foreach (var subscription in subscriptions.ToList())
            {
                try
                {
                    await _pushNotificationService.SendAsync(subscription, calendarEvent.Title, body, cancellationToken);
                }
                catch (PushSubscriptionExpiredException)
                {
                    _context.PushSubscriptions.Remove(subscription);
                    subscriptions.Remove(subscription);
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<TimeZoneInfo> GetTimeZoneAsync(string userId, Dictionary<string, TimeZoneInfo> cache)
    {
        if (cache.TryGetValue(userId, out var cached))
        {
            return cached;
        }

        var timeZoneId = await _identityService.GetTimeZoneIdAsync(userId);
        var timeZone = timeZoneId is null ? TimeZoneInfo.Utc : TryFindTimeZone(timeZoneId);

        cache[userId] = timeZone;
        return timeZone;
    }

    private static TimeZoneInfo TryFindTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
