namespace CalendarManager.Domain.Entities;

public class PushSubscription : BaseAuditableEntity
{
    public string UserId { get; set; } = null!;

    public string Endpoint { get; set; } = null!;

    public string P256dhKey { get; set; } = null!;

    public string AuthKey { get; set; } = null!;
}
