using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Application.Reminders.Commands.DispatchEventReminders;
using CalendarManager.Application.UnitTests.TestHelpers;
using CalendarManager.Domain.Entities;
using CalendarManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace CalendarManager.Application.UnitTests.Reminders.Commands;

public class DispatchEventRemindersCommandHandlerTests
{
    private const string UserId = "user-1";
    private static readonly DateTimeOffset Now = new(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);

    private ApplicationDbContext _context = null!;
    private Mock<IPushNotificationService> _pushNotificationService = null!;
    private Mock<IIdentityService> _identityService = null!;
    private DispatchEventRemindersCommandHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _context = ApplicationDbContextFactory.Create();
        _pushNotificationService = new Mock<IPushNotificationService>();
        _identityService = new Mock<IIdentityService>();
        _identityService.Setup(i => i.GetTimeZoneIdAsync(It.IsAny<string>())).ReturnsAsync((string?)null);

        _handler = new DispatchEventRemindersCommandHandler(_context, _pushNotificationService.Object, _identityService.Object, new FixedTimeProvider(Now));
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    private async Task<PushSubscription> AddSubscriptionAsync(string userId = UserId)
    {
        var subscription = new PushSubscription
        {
            UserId = userId,
            Endpoint = "https://push.example.com/x",
            P256dhKey = "p256dh",
            AuthKey = "auth"
        };
        _context.PushSubscriptions.Add(subscription);
        await _context.SaveChangesAsync(CancellationToken.None);
        return subscription;
    }

    [Test]
    public async Task EventStartingWithinTheLeadTimeSendsAReminderAndMarksItSent()
    {
        await AddSubscriptionAsync();

        var calendarEvent = new CalendarEvent
        {
            UserId = UserId,
            IsLocal = true,
            Title = "Standup",
            StartUtc = Now.AddMinutes(5),
            EndUtc = Now.AddMinutes(35)
        };
        _context.CalendarEvents.Add(calendarEvent);
        await _context.SaveChangesAsync(CancellationToken.None);

        await _handler.Handle(new DispatchEventRemindersCommand(), CancellationToken.None);

        _pushNotificationService.Verify(p => p.SendAsync(
            It.IsAny<PushSubscription>(), "Standup", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        var updated = await _context.CalendarEvents.SingleAsync();
        updated.ReminderSentAtUtc.ShouldBe(Now);
    }

    [Test]
    public async Task EventOutsideTheLeadTimeIsIgnored()
    {
        await AddSubscriptionAsync();

        _context.CalendarEvents.Add(new CalendarEvent
        {
            UserId = UserId,
            IsLocal = true,
            Title = "Next week",
            StartUtc = Now.AddDays(3),
            EndUtc = Now.AddDays(3).AddHours(1)
        });
        await _context.SaveChangesAsync(CancellationToken.None);

        await _handler.Handle(new DispatchEventRemindersCommand(), CancellationToken.None);

        _pushNotificationService.Verify(p => p.SendAsync(
            It.IsAny<PushSubscription>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        var untouched = await _context.CalendarEvents.SingleAsync();
        untouched.ReminderSentAtUtc.ShouldBeNull();
    }

    [Test]
    public async Task EventThatAlreadyHadAReminderSentIsNotResent()
    {
        await AddSubscriptionAsync();

        _context.CalendarEvents.Add(new CalendarEvent
        {
            UserId = UserId,
            IsLocal = true,
            Title = "Already reminded",
            StartUtc = Now.AddMinutes(5),
            EndUtc = Now.AddMinutes(35),
            ReminderSentAtUtc = Now.AddMinutes(-1)
        });
        await _context.SaveChangesAsync(CancellationToken.None);

        await _handler.Handle(new DispatchEventRemindersCommand(), CancellationToken.None);

        _pushNotificationService.Verify(p => p.SendAsync(
            It.IsAny<PushSubscription>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ReminderBodyUsesTheUsersTimeZoneWhenSet()
    {
        await AddSubscriptionAsync();
        _identityService.Setup(i => i.GetTimeZoneIdAsync(UserId)).ReturnsAsync("America/Chicago");

        _context.CalendarEvents.Add(new CalendarEvent
        {
            UserId = UserId,
            IsLocal = true,
            Title = "Standup",
            StartUtc = Now.AddMinutes(5),
            EndUtc = Now.AddMinutes(35)
        });
        await _context.SaveChangesAsync(CancellationToken.None);

        string? capturedBody = null;
        _pushNotificationService
            .Setup(p => p.SendAsync(It.IsAny<PushSubscription>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<PushSubscription, string, string, CancellationToken>((_, _, body, _) => capturedBody = body)
            .Returns(Task.CompletedTask);

        await _handler.Handle(new DispatchEventRemindersCommand(), CancellationToken.None);

        capturedBody.ShouldNotBeNull();
        capturedBody!.ShouldContain("America/Chicago");
        capturedBody.ShouldNotContain("UTC");
    }

    [Test]
    public async Task ExpiredSubscriptionIsRemovedInsteadOfFailingTheWholeDispatch()
    {
        var subscription = await AddSubscriptionAsync();

        _context.CalendarEvents.Add(new CalendarEvent
        {
            UserId = UserId,
            IsLocal = true,
            Title = "Standup",
            StartUtc = Now.AddMinutes(5),
            EndUtc = Now.AddMinutes(35)
        });
        await _context.SaveChangesAsync(CancellationToken.None);

        _pushNotificationService
            .Setup(p => p.SendAsync(It.IsAny<PushSubscription>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PushSubscriptionExpiredException("gone"));

        await _handler.Handle(new DispatchEventRemindersCommand(), CancellationToken.None);

        (await _context.PushSubscriptions.CountAsync()).ShouldBe(0);
        var updated = await _context.CalendarEvents.SingleAsync();
        updated.ReminderSentAtUtc.ShouldBe(Now);
    }
}
