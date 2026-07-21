using CalendarManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CalendarManager.Application.UnitTests.TestHelpers;

public static class ApplicationDbContextFactory
{
    /// <summary>Creates a fresh, isolated in-memory <see cref="ApplicationDbContext"/> for a single test.</summary>
    public static ApplicationDbContext Create()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
