using Microsoft.AspNetCore.Identity;

namespace CalendarManager.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    /// <summary>IANA time zone id (e.g. "America/Chicago"). Null means unset — treated as UTC.</summary>
    public string? TimeZoneId { get; set; }
}
