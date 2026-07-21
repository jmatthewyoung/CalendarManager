using CalendarManager.Domain.Enums;

namespace CalendarManager.Application.Common.Interfaces;

public record OAuthState(string UserId, CalendarProvider Provider);

/// <summary>
/// Short-lived, single-use CSRF state for the calendar-connect OAuth redirect flow.
/// </summary>
public interface IOAuthStateStore
{
    string Create(string userId, CalendarProvider provider);

    /// <summary>Validates and consumes <paramref name="state"/>. Returns null if unknown, expired, or already used.</summary>
    OAuthState? Validate(string state);
}
