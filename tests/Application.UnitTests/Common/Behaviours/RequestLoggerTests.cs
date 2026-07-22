using CalendarManager.Application.CalendarConnections.Commands.SetCalendarConnectionColor;
using CalendarManager.Application.Common.Behaviours;
using CalendarManager.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace CalendarManager.Application.UnitTests.Common.Behaviours;

public class RequestLoggerTests
{
    private Mock<ILogger<SetCalendarConnectionColorCommand>> _logger = null!;
    private Mock<IUser> _user = null!;
    private Mock<IIdentityService> _identityService = null!;

    [SetUp]
    public void Setup()
    {
        _logger = new Mock<ILogger<SetCalendarConnectionColorCommand>>();
        _user = new Mock<IUser>();
        _identityService = new Mock<IIdentityService>();
    }

    [Test]
    public async Task ShouldCallGetUserNameAsyncOnceIfAuthenticated()
    {
        _user.Setup(x => x.Id).Returns(Guid.NewGuid().ToString());

        var requestLogger = new LoggingBehaviour<SetCalendarConnectionColorCommand>(_logger.Object, _user.Object, _identityService.Object);

        await requestLogger.Process(new SetCalendarConnectionColorCommand { Id = 1, Colour = "#ffffff" }, new CancellationToken());

        _identityService.Verify(i => i.GetUserNameAsync(It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task ShouldNotCallGetUserNameAsyncOnceIfUnauthenticated()
    {
        var requestLogger = new LoggingBehaviour<SetCalendarConnectionColorCommand>(_logger.Object, _user.Object, _identityService.Object);

        await requestLogger.Process(new SetCalendarConnectionColorCommand { Id = 1, Colour = "#ffffff" }, new CancellationToken());

        _identityService.Verify(i => i.GetUserNameAsync(It.IsAny<string>()), Times.Never);
    }
}
