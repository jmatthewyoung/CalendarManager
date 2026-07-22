using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Application.Sync.Commands.SyncCalendarConnection;
using CalendarManager.Application.UnitTests.TestHelpers;
using CalendarManager.Domain.Entities;
using CalendarManager.Domain.Enums;
using CalendarManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace CalendarManager.Application.UnitTests.Sync.Commands;

public class SyncCalendarConnectionCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);

    private ApplicationDbContext _context = null!;
    private Mock<ICalendarProviderClient> _client = null!;
    private Mock<ICalendarProviderClientFactory> _clientFactory = null!;
    private Mock<IRefreshTokenProtector> _tokenProtector = null!;
    private Mock<IPushNotificationService> _pushNotificationService = null!;
    private SyncCalendarConnectionCommandHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _context = ApplicationDbContextFactory.Create();

        _client = new Mock<ICalendarProviderClient>();
        _client.Setup(c => c.Provider).Returns(CalendarProvider.Google);

        _clientFactory = new Mock<ICalendarProviderClientFactory>();
        _clientFactory.Setup(f => f.Get(CalendarProvider.Google)).Returns(_client.Object);

        _tokenProtector = new Mock<IRefreshTokenProtector>();
        _tokenProtector.Setup(t => t.Unprotect(It.IsAny<string>())).Returns("plaintext-refresh-token");

        _pushNotificationService = new Mock<IPushNotificationService>();

        _handler = new SyncCalendarConnectionCommandHandler(
            _context,
            _clientFactory.Object,
            _tokenProtector.Object,
            _pushNotificationService.Object,
            new FixedTimeProvider(Now));
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    private async Task<CalendarConnection> AddConnectionAsync(bool needsReauth = false)
    {
        var connection = new CalendarConnection
        {
            UserId = "user-1",
            Provider = CalendarProvider.Google,
            AccountEmail = "someone@example.com",
            EncryptedRefreshToken = "encrypted",
            NeedsReauth = needsReauth
        };

        _context.CalendarConnections.Add(connection);
        await _context.SaveChangesAsync(CancellationToken.None);

        return connection;
    }

    [Test]
    public async Task NewProviderEventIsInsertedAndLogged()
    {
        var connection = await AddConnectionAsync();

        _client.Setup(c => c.GetEventsAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ProviderEvent("ext-1", "Standup", Now.AddHours(1), Now.AddHours(2), false, null, null, [])]);

        await _handler.Handle(new SyncCalendarConnectionCommand(connection.Id), CancellationToken.None);

        var events = await _context.CalendarEvents.ToListAsync();
        events.Count.ShouldBe(1);
        events[0].ExternalEventId.ShouldBe("ext-1");
        events[0].Title.ShouldBe("Standup");
        events[0].UserId.ShouldBe(connection.UserId);

        var log = await _context.SyncLogs.SingleAsync();
        log.Status.ShouldBe(SyncStatus.Success);
        log.EventsAdded.ShouldBe(1);
        log.EventsUpdated.ShouldBe(0);
        log.EventsRemoved.ShouldBe(0);

        connection.LastSyncedAtUtc.ShouldBe(Now);
        connection.NeedsReauth.ShouldBeFalse();
    }

    [Test]
    public async Task ChangedProviderEventUpdatesTheExistingRow()
    {
        var connection = await AddConnectionAsync();
        _context.CalendarEvents.Add(new CalendarEvent
        {
            CalendarConnectionId = connection.Id,
            UserId = connection.UserId,
            ExternalEventId = "ext-1",
            Title = "Old title",
            StartUtc = Now,
            EndUtc = Now.AddHours(1)
        });
        await _context.SaveChangesAsync(CancellationToken.None);

        _client.Setup(c => c.GetEventsAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ProviderEvent("ext-1", "New title", Now.AddHours(1), Now.AddHours(2), false, null, null, [])]);

        await _handler.Handle(new SyncCalendarConnectionCommand(connection.Id), CancellationToken.None);

        var updated = await _context.CalendarEvents.SingleAsync();
        updated.Title.ShouldBe("New title");
        updated.StartUtc.ShouldBe(Now.AddHours(1));

        var log = await _context.SyncLogs.SingleAsync();
        log.EventsUpdated.ShouldBe(1);
        log.EventsAdded.ShouldBe(0);
    }

    [Test]
    public async Task NewProviderEventPersistsOrganizerAndAttendees()
    {
        var connection = await AddConnectionAsync();

        _client.Setup(c => c.GetEventsAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ProviderEvent("ext-1", "Standup", Now.AddHours(1), Now.AddHours(2), false,
                "boss@example.com", "The Boss",
                [new ProviderAttendee("teammate@example.com", "Teammate", AttendeeResponseStatus.Accepted)])]);

        await _handler.Handle(new SyncCalendarConnectionCommand(connection.Id), CancellationToken.None);

        var saved = await _context.CalendarEvents.Include(e => e.Attendees).SingleAsync();
        saved.OrganizerEmail.ShouldBe("boss@example.com");
        saved.OrganizerName.ShouldBe("The Boss");
        saved.Attendees.Count.ShouldBe(1);
        saved.Attendees.Single().Email.ShouldBe("teammate@example.com");
        saved.Attendees.Single().ResponseStatus.ShouldBe(AttendeeResponseStatus.Accepted);
    }

    [Test]
    public async Task ChangedAttendeeResponseStatusUpdatesTheExistingRow()
    {
        var connection = await AddConnectionAsync();
        var existing = new CalendarEvent
        {
            CalendarConnectionId = connection.Id,
            UserId = connection.UserId,
            ExternalEventId = "ext-1",
            Title = "Standup",
            StartUtc = Now.AddHours(1),
            EndUtc = Now.AddHours(2),
            Attendees = [new EventAttendee { Email = "teammate@example.com", Name = "Teammate", ResponseStatus = AttendeeResponseStatus.NeedsAction }]
        };
        _context.CalendarEvents.Add(existing);
        await _context.SaveChangesAsync(CancellationToken.None);

        _client.Setup(c => c.GetEventsAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ProviderEvent("ext-1", "Standup", Now.AddHours(1), Now.AddHours(2), false,
                null, null,
                [new ProviderAttendee("teammate@example.com", "Teammate", AttendeeResponseStatus.Declined)])]);

        await _handler.Handle(new SyncCalendarConnectionCommand(connection.Id), CancellationToken.None);

        var updated = await _context.CalendarEvents.Include(e => e.Attendees).SingleAsync();
        updated.Attendees.Single().ResponseStatus.ShouldBe(AttendeeResponseStatus.Declined);

        var log = await _context.SyncLogs.SingleAsync();
        log.EventsUpdated.ShouldBe(1);
    }

    [Test]
    public async Task EventNoLongerReturnedByProviderIsRemoved()
    {
        var connection = await AddConnectionAsync();
        _context.CalendarEvents.Add(new CalendarEvent
        {
            CalendarConnectionId = connection.Id,
            UserId = connection.UserId,
            ExternalEventId = "ext-1",
            Title = "Gone soon",
            StartUtc = Now,
            EndUtc = Now.AddHours(1)
        });
        await _context.SaveChangesAsync(CancellationToken.None);

        _client.Setup(c => c.GetEventsAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _handler.Handle(new SyncCalendarConnectionCommand(connection.Id), CancellationToken.None);

        (await _context.CalendarEvents.CountAsync()).ShouldBe(0);

        var log = await _context.SyncLogs.SingleAsync();
        log.EventsRemoved.ShouldBe(1);
    }

    [Test]
    public async Task LocalEventsAreNeverTouchedByASync()
    {
        var connection = await AddConnectionAsync();
        _context.CalendarEvents.Add(new CalendarEvent
        {
            CalendarConnectionId = null,
            UserId = connection.UserId,
            IsLocal = true,
            Title = "My local event",
            StartUtc = Now,
            EndUtc = Now.AddHours(1)
        });
        await _context.SaveChangesAsync(CancellationToken.None);

        _client.Setup(c => c.GetEventsAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _handler.Handle(new SyncCalendarConnectionCommand(connection.Id), CancellationToken.None);

        var remaining = await _context.CalendarEvents.SingleAsync();
        remaining.IsLocal.ShouldBeTrue();
        remaining.Title.ShouldBe("My local event");
    }

    [Test]
    public async Task AuthFailureFlagsTheConnectionAndNotifiesSubscribersOnce()
    {
        var connection = await AddConnectionAsync();
        _context.PushSubscriptions.Add(new PushSubscription
        {
            UserId = connection.UserId,
            Endpoint = "https://push.example.com/abc",
            P256dhKey = "p256dh",
            AuthKey = "auth"
        });
        await _context.SaveChangesAsync(CancellationToken.None);

        _client.Setup(c => c.GetEventsAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CalendarAuthException("token revoked"));

        await _handler.Handle(new SyncCalendarConnectionCommand(connection.Id), CancellationToken.None);

        connection.NeedsReauth.ShouldBeTrue();

        var log = await _context.SyncLogs.SingleAsync();
        log.Status.ShouldBe(SyncStatus.AuthExpired);

        _pushNotificationService.Verify(p => p.SendAsync(
            It.IsAny<PushSubscription>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RepeatedAuthFailureDoesNotSendASecondNotification()
    {
        var connection = await AddConnectionAsync(needsReauth: true);

        _client.Setup(c => c.GetEventsAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CalendarAuthException("still revoked"));

        await _handler.Handle(new SyncCalendarConnectionCommand(connection.Id), CancellationToken.None);

        _pushNotificationService.Verify(p => p.SendAsync(
            It.IsAny<PushSubscription>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task UnexpectedFailureIsLoggedWithoutFlaggingReauth()
    {
        var connection = await AddConnectionAsync();

        _client.Setup(c => c.GetEventsAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("provider is down"));

        await _handler.Handle(new SyncCalendarConnectionCommand(connection.Id), CancellationToken.None);

        connection.NeedsReauth.ShouldBeFalse();

        var log = await _context.SyncLogs.SingleAsync();
        log.Status.ShouldBe(SyncStatus.Failed);
        log.Message.ShouldBe("provider is down");
    }

    [Test]
    public void MissingConnectionThrowsNotFound()
    {
        Should.ThrowAsync<Ardalis.GuardClauses.NotFoundException>(() =>
            _handler.Handle(new SyncCalendarConnectionCommand(999), CancellationToken.None));
    }
}
