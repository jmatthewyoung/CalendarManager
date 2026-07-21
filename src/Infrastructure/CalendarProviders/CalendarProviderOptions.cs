namespace CalendarManager.Infrastructure.CalendarProviders;

public class GoogleCalendarOptions
{
    public const string SectionName = "CalendarProviders:Google";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;
}

public class OutlookCalendarOptions
{
    public const string SectionName = "CalendarProviders:Outlook";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string TenantId { get; set; } = "common";
}
