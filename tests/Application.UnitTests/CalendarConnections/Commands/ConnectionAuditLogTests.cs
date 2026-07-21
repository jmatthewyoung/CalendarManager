using CalendarManager.Application.CalendarConnections.Commands.CompleteCalendarConnection;
using CalendarManager.Application.CalendarConnections.Commands.DisconnectCalendarConnection;
using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Application.UnitTests.TestHelpers;
using CalendarManager.Domain.Entities;
using CalendarManager.Domain.Enums;
using CalendarManager.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace CalendarManager.Application.UnitTests.CalendarConnections.Commands;

public class ConnectionAuditLogTests
{
    private const string UserId = "user-1";
    private static readonly DateTimeOffset Now = new(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);

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
    public async Task CompletingAConnectionWritesAConnectedAuditEntry()
    {
        var stateStore = new Mock<IOAuthStateStore>();
        stateStore.Setup(s => s.Validate("state-token")).Returns(new OAuthState(UserId, CalendarProvider.Google));

        var client = new Mock<ICalendarProviderClient>();
        client.Setup(c => c.ExchangeAuthorizationCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalTokenResponse("access", "refresh", Now.AddHours(1)));
        client.Setup(c => c.GetAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("someone@example.com");

        var clientFactory = new Mock<ICalendarProviderClientFactory>();
        clientFactory.Setup(f => f.Get(CalendarProvider.Google)).Returns(client.Object);

        var tokenProtector = new Mock<IRefreshTokenProtector>();
        tokenProtector.Setup(t => t.Protect(It.IsAny<string>())).Returns("encrypted");

        var sender = new Mock<ISender>();

        var handler = new CompleteCalendarConnectionCommandHandler(
            _context, _user.Object, stateStore.Object, clientFactory.Object, tokenProtector.Object, sender.Object,
            new FixedTimeProvider(Now));

        await handler.Handle(new CompleteCalendarConnectionCommand(CalendarProvider.Google, "code", "state-token", "https://app/callback"), CancellationToken.None);

        var entry = await _context.ConnectionAuditLogs.SingleAsync();
        entry.UserId.ShouldBe(UserId);
        entry.Provider.ShouldBe(CalendarProvider.Google);
        entry.AccountEmail.ShouldBe("someone@example.com");
        entry.Action.ShouldBe(ConnectionAuditAction.Connected);
        entry.OccurredAtUtc.ShouldBe(Now);
    }

    [Test]
    public async Task DisconnectingWritesADisconnectedAuditEntryEvenThoughTheConnectionRowIsGone()
    {
        var connection = new CalendarConnection
        {
            UserId = UserId,
            Provider = CalendarProvider.Outlook,
            AccountEmail = "someone@example.com",
            EncryptedRefreshToken = "token"
        };
        _context.CalendarConnections.Add(connection);
        await _context.SaveChangesAsync(CancellationToken.None);

        var handler = new DisconnectCalendarConnectionCommandHandler(_context, _user.Object, new FixedTimeProvider(Now));

        await handler.Handle(new DisconnectCalendarConnectionCommand(connection.Id), CancellationToken.None);

        (await _context.CalendarConnections.CountAsync()).ShouldBe(0);

        var entry = await _context.ConnectionAuditLogs.SingleAsync();
        entry.Provider.ShouldBe(CalendarProvider.Outlook);
        entry.AccountEmail.ShouldBe("someone@example.com");
        entry.Action.ShouldBe(ConnectionAuditAction.Disconnected);
        entry.OccurredAtUtc.ShouldBe(Now);
    }
}
