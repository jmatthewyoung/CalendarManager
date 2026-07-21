using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Domain.Enums;

namespace CalendarManager.Infrastructure.CalendarProviders;

public class CalendarProviderClientFactory : ICalendarProviderClientFactory
{
    private readonly IEnumerable<ICalendarProviderClient> _clients;

    public CalendarProviderClientFactory(IEnumerable<ICalendarProviderClient> clients)
    {
        _clients = clients;
    }

    public ICalendarProviderClient Get(CalendarProvider provider)
    {
        var client = _clients.SingleOrDefault(c => c.Provider == provider);

        return client ?? throw new NotSupportedException($"No calendar provider client registered for '{provider}'.");
    }
}
