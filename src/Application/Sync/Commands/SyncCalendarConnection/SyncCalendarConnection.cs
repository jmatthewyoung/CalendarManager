using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Domain.Entities;
using CalendarManager.Domain.Enums;

namespace CalendarManager.Application.Sync.Commands.SyncCalendarConnection;

public record SyncCalendarConnectionCommand(int CalendarConnectionId) : IRequest;

public class SyncCalendarConnectionCommandHandler : IRequestHandler<SyncCalendarConnectionCommand>
{
    private static readonly TimeSpan SyncWindow = TimeSpan.FromDays(183);

    private readonly IApplicationDbContext _context;
    private readonly ICalendarProviderClientFactory _clientFactory;
    private readonly IRefreshTokenProtector _tokenProtector;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly TimeProvider _timeProvider;

    public SyncCalendarConnectionCommandHandler(
        IApplicationDbContext context,
        ICalendarProviderClientFactory clientFactory,
        IRefreshTokenProtector tokenProtector,
        IPushNotificationService pushNotificationService,
        TimeProvider timeProvider)
    {
        _context = context;
        _clientFactory = clientFactory;
        _tokenProtector = tokenProtector;
        _pushNotificationService = pushNotificationService;
        _timeProvider = timeProvider;
    }

    public async Task Handle(SyncCalendarConnectionCommand request, CancellationToken cancellationToken)
    {
        var connection = await _context.CalendarConnections
            .SingleOrDefaultAsync(c => c.Id == request.CalendarConnectionId, cancellationToken);

        Guard.Against.NotFound(request.CalendarConnectionId, connection);

        var now = _timeProvider.GetUtcNow();
        var windowStart = now - SyncWindow;
        var windowEnd = now + SyncWindow;

        var log = new SyncLog
        {
            CalendarConnectionId = connection.Id,
            RanAtUtc = now
        };

        try
        {
            var client = _clientFactory.Get(connection.Provider);
            var refreshToken = _tokenProtector.Unprotect(connection.EncryptedRefreshToken);
            var providerEvents = await client.GetEventsAsync(refreshToken, windowStart, windowEnd, cancellationToken);

            var existingEvents = await _context.CalendarEvents
                .Include(e => e.Attendees)
                .Where(e => e.CalendarConnectionId == connection.Id)
                .ToListAsync(cancellationToken);

            var existingByExternalId = existingEvents.ToDictionary(e => e.ExternalEventId!);
            var providerEventIds = providerEvents.Select(e => e.ExternalEventId).ToHashSet();

            foreach (var providerEvent in providerEvents)
            {
                if (existingByExternalId.TryGetValue(providerEvent.ExternalEventId, out var existing))
                {
                    var attendeesChanged = !AttendeesEqual(existing.Attendees, providerEvent.Attendees);

                    if (existing.Title != providerEvent.Title
                        || existing.StartUtc != providerEvent.StartUtc
                        || existing.EndUtc != providerEvent.EndUtc
                        || existing.IsAllDay != providerEvent.IsAllDay
                        || existing.OrganizerEmail != providerEvent.OrganizerEmail
                        || existing.OrganizerName != providerEvent.OrganizerName
                        || attendeesChanged)
                    {
                        existing.Title = providerEvent.Title;
                        existing.StartUtc = providerEvent.StartUtc;
                        existing.EndUtc = providerEvent.EndUtc;
                        existing.IsAllDay = providerEvent.IsAllDay;
                        existing.OrganizerEmail = providerEvent.OrganizerEmail;
                        existing.OrganizerName = providerEvent.OrganizerName;

                        if (attendeesChanged)
                        {
                            existing.Attendees.Clear();
                            foreach (var attendee in providerEvent.Attendees)
                            {
                                existing.Attendees.Add(new EventAttendee
                                {
                                    Email = attendee.Email,
                                    Name = attendee.Name,
                                    ResponseStatus = attendee.ResponseStatus
                                });
                            }
                        }

                        log.EventsUpdated++;
                    }
                }
                else
                {
                    _context.CalendarEvents.Add(new CalendarEvent
                    {
                        CalendarConnectionId = connection.Id,
                        UserId = connection.UserId,
                        ExternalEventId = providerEvent.ExternalEventId,
                        Title = providerEvent.Title,
                        StartUtc = providerEvent.StartUtc,
                        EndUtc = providerEvent.EndUtc,
                        IsAllDay = providerEvent.IsAllDay,
                        OrganizerEmail = providerEvent.OrganizerEmail,
                        OrganizerName = providerEvent.OrganizerName,
                        Attendees = providerEvent.Attendees.Select(a => new EventAttendee
                        {
                            Email = a.Email,
                            Name = a.Name,
                            ResponseStatus = a.ResponseStatus
                        }).ToList()
                    });
                    log.EventsAdded++;
                }
            }

            var removedEvents = existingEvents.Where(e => !providerEventIds.Contains(e.ExternalEventId!));
            foreach (var removed in removedEvents)
            {
                _context.CalendarEvents.Remove(removed);
                log.EventsRemoved++;
            }

            connection.LastSyncedAtUtc = now;
            connection.NeedsReauth = false;
            log.Status = SyncStatus.Success;
        }
        catch (CalendarAuthException ex)
        {
            var wasAlreadyFlagged = connection.NeedsReauth;
            connection.NeedsReauth = true;
            log.Status = SyncStatus.AuthExpired;
            log.Message = ex.Message;

            if (!wasAlreadyFlagged)
            {
                await NotifyReauthRequiredAsync(connection, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            log.Status = SyncStatus.Failed;
            log.Message = ex.Message;
        }

        _context.SyncLogs.Add(log);

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static bool AttendeesEqual(ICollection<EventAttendee> existing, IReadOnlyList<ProviderAttendee> incoming)
    {
        if (existing.Count != incoming.Count) return false;

        var existingSet = existing
            .Select(a => (a.Email, a.Name, a.ResponseStatus))
            .OrderBy(a => a.Email, StringComparer.Ordinal)
            .ToList();

        var incomingSet = incoming
            .Select(a => (a.Email, a.Name, a.ResponseStatus))
            .OrderBy(a => a.Email, StringComparer.Ordinal)
            .ToList();

        return existingSet.SequenceEqual(incomingSet);
    }

    private async Task NotifyReauthRequiredAsync(CalendarConnection connection, CancellationToken cancellationToken)
    {
        var subscriptions = await _context.PushSubscriptions
            .Where(p => p.UserId == connection.UserId)
            .ToListAsync(cancellationToken);

        foreach (var subscription in subscriptions)
        {
            try
            {
                await _pushNotificationService.SendAsync(
                    subscription,
                    "Calendar needs reconnecting",
                    $"{connection.AccountEmail} stopped syncing. Reconnect it to resume.",
                    cancellationToken);
            }
            catch (PushSubscriptionExpiredException)
            {
                _context.PushSubscriptions.Remove(subscription);
            }
        }
    }
}
