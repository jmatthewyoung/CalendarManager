using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Application.Events.Commands.DeleteLocalEvent;
using CalendarManager.Application.Events.Commands.SetEventColorOverride;
using CalendarManager.Application.Events.Commands.UpdateLocalEvent;
using CalendarManager.Application.UnitTests.TestHelpers;
using CalendarManager.Domain.Entities;
using CalendarManager.Domain.Enums;
using CalendarManager.Domain.ValueObjects;
using CalendarManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace CalendarManager.Application.UnitTests.Events.Commands;

public class LocalEventCommandTests
{
    private const string UserId = "user-1";
    private const string OtherUserId = "user-2";

    private ApplicationDbContext _context = null!;
    private Mock<IUser> _user = null!;

    [SetUp]
    public void Setup()
    {
        _context = ApplicationDbContextFactory.Create();

        _user = new Mock<IUser>();
        _user.Setup(u => u.Id).Returns(UserId);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public async Task UpdateLocalEventChangesFieldsOnTheOwnedEvent()
    {
        var localEvent = await AddLocalEventAsync(UserId);

        var handler = new UpdateLocalEventCommandHandler(_context, _user.Object);

        await handler.Handle(new UpdateLocalEventCommand
        {
            Id = localEvent.Id,
            Title = "Renamed",
            StartUtc = localEvent.StartUtc.AddHours(1),
            EndUtc = localEvent.EndUtc.AddHours(1),
            IsAllDay = true,
            Colour = Colour.Red.Code
        }, CancellationToken.None);

        var updated = await _context.CalendarEvents.SingleAsync(e => e.Id == localEvent.Id);
        updated.Title.ShouldBe("Renamed");
        updated.IsAllDay.ShouldBeTrue();
        updated.ColourOverride!.Code.ShouldBe(Colour.Red.Code);
    }

    [Test]
    public void UpdateLocalEventOwnedByAnotherUserThrowsNotFound()
    {
        var handler = new UpdateLocalEventCommandHandler(_context, _user.Object);

        Should.ThrowAsync<Ardalis.GuardClauses.NotFoundException>(async () =>
        {
            var localEvent = await AddLocalEventAsync(OtherUserId);

            await handler.Handle(new UpdateLocalEventCommand
            {
                Id = localEvent.Id,
                Title = "Hijacked",
                StartUtc = localEvent.StartUtc,
                EndUtc = localEvent.EndUtc,
                Colour = Colour.Red.Code
            }, CancellationToken.None);
        });
    }

    [Test]
    public async Task DeleteLocalEventRemovesTheRow()
    {
        var localEvent = await AddLocalEventAsync(UserId);
        var handler = new DeleteLocalEventCommandHandler(_context, _user.Object);

        await handler.Handle(new DeleteLocalEventCommand(localEvent.Id), CancellationToken.None);

        (await _context.CalendarEvents.CountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task DeleteLocalEventCannotDeleteASyncedEvent()
    {
        var connection = new CalendarConnection
        {
            UserId = UserId,
            Provider = CalendarProvider.Google,
            AccountEmail = "me@example.com",
            EncryptedRefreshToken = "token"
        };
        _context.CalendarConnections.Add(connection);
        await _context.SaveChangesAsync(CancellationToken.None);

        var syncedEvent = new CalendarEvent
        {
            CalendarConnectionId = connection.Id,
            UserId = UserId,
            ExternalEventId = "ext-1",
            Title = "Synced meeting",
            StartUtc = DateTimeOffset.UtcNow,
            EndUtc = DateTimeOffset.UtcNow.AddHours(1),
            IsLocal = false
        };
        _context.CalendarEvents.Add(syncedEvent);
        await _context.SaveChangesAsync(CancellationToken.None);

        var handler = new DeleteLocalEventCommandHandler(_context, _user.Object);

        await Should.ThrowAsync<Ardalis.GuardClauses.NotFoundException>(() =>
            handler.Handle(new DeleteLocalEventCommand(syncedEvent.Id), CancellationToken.None));

        (await _context.CalendarEvents.CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task SetEventColorOverrideWorksOnASyncedEventWithoutChangingItsConnectionColour()
    {
        var connection = new CalendarConnection
        {
            UserId = UserId,
            Provider = CalendarProvider.Google,
            AccountEmail = "me@example.com",
            EncryptedRefreshToken = "token",
            Colour = Colour.Grey
        };
        _context.CalendarConnections.Add(connection);
        await _context.SaveChangesAsync(CancellationToken.None);

        var syncedEvent = new CalendarEvent
        {
            CalendarConnectionId = connection.Id,
            UserId = UserId,
            ExternalEventId = "ext-1",
            Title = "Synced meeting",
            StartUtc = DateTimeOffset.UtcNow,
            EndUtc = DateTimeOffset.UtcNow.AddHours(1)
        };
        _context.CalendarEvents.Add(syncedEvent);
        await _context.SaveChangesAsync(CancellationToken.None);

        var handler = new SetEventColorOverrideCommandHandler(_context, _user.Object);

        await handler.Handle(new SetEventColorOverrideCommand { Id = syncedEvent.Id, Colour = Colour.Purple.Code }, CancellationToken.None);

        var updated = await _context.CalendarEvents.SingleAsync(e => e.Id == syncedEvent.Id);
        updated.ColourOverride!.Code.ShouldBe(Colour.Purple.Code);

        var unchangedConnection = await _context.CalendarConnections.SingleAsync();
        unchangedConnection.Colour.Code.ShouldBe(Colour.Grey.Code);
    }

    private async Task<CalendarEvent> AddLocalEventAsync(string userId)
    {
        var entity = new CalendarEvent
        {
            UserId = userId,
            IsLocal = true,
            Title = "Original",
            StartUtc = new DateTimeOffset(2026, 7, 22, 9, 0, 0, TimeSpan.Zero),
            EndUtc = new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero),
            ColourOverride = Colour.Green
        };

        _context.CalendarEvents.Add(entity);
        await _context.SaveChangesAsync(CancellationToken.None);

        return entity;
    }
}
