namespace CalendarManager.Infrastructure.Push;

public class WebPushOptions
{
    public const string SectionName = "WebPush";

    public string PublicKey { get; set; } = string.Empty;

    public string PrivateKey { get; set; } = string.Empty;

    /// <summary>A "mailto:" address or contact URL, required by the VAPID spec.</summary>
    public string Subject { get; set; } = string.Empty;
}
