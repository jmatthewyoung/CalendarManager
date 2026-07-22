using System.Reflection;
using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Domain.Entities;
using CalendarManager.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CalendarManager.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<CalendarConnection> CalendarConnections => Set<CalendarConnection>();

    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();

    public DbSet<EventAttendee> EventAttendees => Set<EventAttendee>();

    public DbSet<SyncLog> SyncLogs => Set<SyncLog>();

    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();

    public DbSet<ConnectionAuditLog> ConnectionAuditLogs => Set<ConnectionAuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
