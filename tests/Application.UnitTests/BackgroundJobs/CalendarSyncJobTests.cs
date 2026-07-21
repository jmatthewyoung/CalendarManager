using CalendarManager.Application.Sync.Commands.SyncCalendarConnection;
using CalendarManager.Application.UnitTests.TestHelpers;
using CalendarManager.Domain.Entities;
using CalendarManager.Domain.Enums;
using CalendarManager.Infrastructure.BackgroundJobs;
using CalendarManager.Infrastructure.Data;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Quartz;
using Shouldly;

namespace CalendarManager.Application.UnitTests.BackgroundJobs;

public class CalendarSyncJobTests
{
    private ApplicationDbContext _context = null!;
    private Mock<ISender> _sender = null!;
    private CalendarSyncJob _job = null!;

    [SetUp]
    public void Setup()
    {
        _context = ApplicationDbContextFactory.Create();
        _sender = new Mock<ISender>();
        _job = new CalendarSyncJob(_context, _sender.Object, NullLogger<CalendarSyncJob>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    private static Mock<IJobExecutionContext> CreateJobContext()
    {
        var context = new Mock<IJobExecutionContext>();
        context.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        return context;
    }

    private async Task<CalendarConnection> AddConnectionAsync(bool needsReauth = false)
    {
        var connection = new CalendarConnection
        {
            UserId = "user-1",
            Provider = CalendarProvider.Google,
            AccountEmail = $"{Guid.NewGuid()}@example.com",
            EncryptedRefreshToken = "token",
            NeedsReauth = needsReauth
        };
        _context.CalendarConnections.Add(connection);
        await _context.SaveChangesAsync(CancellationToken.None);
        return connection;
    }

    [Test]
    public async Task OneConnectionThrowingDoesNotStopTheRestOfTheTick()
    {
        var failing = await AddConnectionAsync();
        var succeeding = await AddConnectionAsync();

        _sender.Setup(s => s.Send(It.Is<SyncCalendarConnectionCommand>(c => c.CalendarConnectionId == failing.Id), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        _sender.Setup(s => s.Send(It.Is<SyncCalendarConnectionCommand>(c => c.CalendarConnectionId == succeeding.Id), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _job.Execute(CreateJobContext().Object);

        _sender.Verify(s => s.Send(It.Is<SyncCalendarConnectionCommand>(c => c.CalendarConnectionId == failing.Id), It.IsAny<CancellationToken>()), Times.Once);
        _sender.Verify(s => s.Send(It.Is<SyncCalendarConnectionCommand>(c => c.CalendarConnectionId == succeeding.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ConnectionsFlaggedNeedsReauthAreSkipped()
    {
        var needsReauth = await AddConnectionAsync(needsReauth: true);
        var active = await AddConnectionAsync();

        await _job.Execute(CreateJobContext().Object);

        _sender.Verify(s => s.Send(It.Is<SyncCalendarConnectionCommand>(c => c.CalendarConnectionId == needsReauth.Id), It.IsAny<CancellationToken>()), Times.Never);
        _sender.Verify(s => s.Send(It.Is<SyncCalendarConnectionCommand>(c => c.CalendarConnectionId == active.Id), It.IsAny<CancellationToken>()), Times.Once);
    }
}
