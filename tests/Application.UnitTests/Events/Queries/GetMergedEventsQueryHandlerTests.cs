using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Application.Events.Queries.GetMergedEvents;
using CalendarManager.Application.UnitTests.TestHelpers;
using CalendarManager.Domain.Entities;
using CalendarManager.Domain.Enums;
using CalendarManager.Domain.ValueObjects;
using CalendarManager.Infrastructure.Data;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace CalendarManager.Application.UnitTests.Events.Queries;

public class GetMergedEventsQueryHandlerTests
{
    private const string UserId = "user-1";
    private static readonly DateTimeOffset RangeStart = new(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RangeEnd = new(2026, 7, 27, 0, 0, 0, TimeSpan.Zero);

    private ApplicationDbContext _context = null!;
    private GetMergedEventsQueryHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _context = ApplicationDbContextFactory.Create();

        var user = new Mock<IUser>();
        user.Setup(u => u.Id).Returns(UserId);

        _handler = new GetMergedEventsQueryHandler(_context, user.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public async Task HiddenConnectionsEventsAreExcludedButLocalEventsAreAlwaysIncluded()
    {
        var hiddenConnection = new CalendarConnection
        {
            UserId = UserId,
            Provider = CalendarProvider.Google,
            AccountEmail = "hidden@example.com",
            EncryptedRefreshToken = "token",
            IsVisible = false,
            Colour = Colour.Blue
        };
        _context.CalendarConnections.Add(hiddenConnection);
        await _context.SaveChangesAsync(CancellationToken.None);

        _context.CalendarEvents.Add(new CalendarEvent
        {
            CalendarConnectionId = hiddenConnection.Id,
            UserId = UserId,
            ExternalEventId = "ext-1",
            Title = "Hidden meeting",
            StartUtc = RangeStart.AddDays(1),
            EndUtc = RangeStart.AddDays(1).AddHours(1)
        });
        _context.CalendarEvents.Add(new CalendarEvent
        {
            UserId = UserId,
            IsLocal = true,
            Title = "My local event",
            StartUtc = RangeStart.AddDays(2),
            EndUtc = RangeStart.AddDays(2).AddHours(1),
            ColourOverride = Colour.Orange
        });
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await _handler.Handle(new GetMergedEventsQuery(RangeStart, RangeEnd), CancellationToken.None);

        result.Count.ShouldBe(1);
        result[0].Title.ShouldBe("My local event");
        result[0].IsLocal.ShouldBeTrue();
        result[0].Provider.ShouldBe(CalendarProvider.Local);
        result[0].Colour.ShouldBe(Colour.Orange.Code);
    }

    [Test]
    public async Task ColourOverrideTakesPrecedenceOverConnectionColour()
    {
        var connection = new CalendarConnection
        {
            UserId = UserId,
            Provider = CalendarProvider.Outlook,
            AccountEmail = "visible@example.com",
            EncryptedRefreshToken = "token",
            IsVisible = true,
            Colour = Colour.Grey
        };
        _context.CalendarConnections.Add(connection);
        await _context.SaveChangesAsync(CancellationToken.None);

        _context.CalendarEvents.Add(new CalendarEvent
        {
            CalendarConnectionId = connection.Id,
            UserId = UserId,
            ExternalEventId = "ext-1",
            Title = "Overridden colour",
            StartUtc = RangeStart.AddDays(1),
            EndUtc = RangeStart.AddDays(1).AddHours(1),
            ColourOverride = Colour.Red
        });
        _context.CalendarEvents.Add(new CalendarEvent
        {
            CalendarConnectionId = connection.Id,
            UserId = UserId,
            ExternalEventId = "ext-2",
            Title = "Default colour",
            StartUtc = RangeStart.AddDays(1),
            EndUtc = RangeStart.AddDays(1).AddHours(1)
        });
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await _handler.Handle(new GetMergedEventsQuery(RangeStart, RangeEnd), CancellationToken.None);

        result.Single(e => e.Title == "Overridden colour").Colour.ShouldBe(Colour.Red.Code);
        result.Single(e => e.Title == "Default colour").Colour.ShouldBe(Colour.Grey.Code);
    }

    [Test]
    public async Task EventsOutsideTheRequestedWindowAreExcluded()
    {
        _context.CalendarEvents.Add(new CalendarEvent
        {
            UserId = UserId,
            IsLocal = true,
            Title = "Too early",
            StartUtc = RangeStart.AddDays(-5),
            EndUtc = RangeStart.AddDays(-5).AddHours(1),
            ColourOverride = Colour.Teal
        });
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await _handler.Handle(new GetMergedEventsQuery(RangeStart, RangeEnd), CancellationToken.None);

        result.ShouldBeEmpty();
    }
}
