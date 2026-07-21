namespace CalendarManager.Domain.Entities;

public class CalendarConnection : BaseAuditableEntity
{
    public string UserId { get; set; } = null!;

    public CalendarProvider Provider { get; set; }

    public string AccountEmail { get; set; } = null!;

    public string EncryptedRefreshToken { get; set; } = null!;

    public Colour Colour { get; set; } = Colour.Grey;

    public bool IsVisible { get; set; } = true;

    public bool NeedsReauth { get; set; }

    public DateTimeOffset? LastSyncedAtUtc { get; set; }

    public IList<CalendarEvent> Events { get; private set; } = new List<CalendarEvent>();
}
