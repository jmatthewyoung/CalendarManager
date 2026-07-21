using CalendarManager.Domain.Entities;
using CalendarManager.Domain.Enums;

namespace CalendarManager.Application.Sync.Queries.GetSyncLogs;

public class SyncLogDto
{
    public int Id { get; init; }

    public int CalendarConnectionId { get; init; }

    public SyncStatus Status { get; init; }

    public string? Message { get; init; }

    public int EventsAdded { get; init; }

    public int EventsUpdated { get; init; }

    public int EventsRemoved { get; init; }

    public DateTimeOffset RanAtUtc { get; init; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<SyncLog, SyncLogDto>();
        }
    }
}
