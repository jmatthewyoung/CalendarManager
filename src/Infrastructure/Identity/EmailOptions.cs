namespace CalendarManager.Infrastructure.Identity;

public class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Azure Communication Services connection string. Left empty in dev — emails are logged instead of sent.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    public string SenderAddress { get; set; } = string.Empty;

    /// <summary>Used to build links when there's no current HTTP request to infer the origin from.</summary>
    public string FallbackBaseUrl { get; set; } = "https://localhost:5001";
}
