using CalendarManager.Application.Reminders.Commands.DispatchEventReminders;
using MediatR;
using Quartz;

namespace CalendarManager.Infrastructure.BackgroundJobs;

/// <summary>
/// Runs every 5 minutes (scheduled in <c>DependencyInjection.AddInfrastructureServices</c>) and
/// dispatches push reminders for events starting soon.
/// </summary>
public class ReminderDispatchJob : IJob
{
    private readonly ISender _sender;

    public ReminderDispatchJob(ISender sender)
    {
        _sender = sender;
    }

    public Task Execute(IJobExecutionContext context)
    {
        return _sender.Send(new DispatchEventRemindersCommand(), context.CancellationToken);
    }
}
