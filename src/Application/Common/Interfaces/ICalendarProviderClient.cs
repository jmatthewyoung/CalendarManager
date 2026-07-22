using CalendarManager.Domain.Enums;

namespace CalendarManager.Application.Common.Interfaces;

public record ExternalTokenResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAtUtc);

public record ProviderAttendee(string Email, string? Name, AttendeeResponseStatus ResponseStatus);

public record ProviderEvent(
    string ExternalEventId,
    string Title,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    bool IsAllDay,
    string? OrganizerEmail,
    string? OrganizerName,
    IReadOnlyList<ProviderAttendee> Attendees);

/// <summary>
/// Thrown by an <see cref="ICalendarProviderClient"/> when a call fails because the stored
/// refresh token has been revoked or expired, so the caller knows to flag the connection as
/// needing reauthorization rather than treating it as a transient sync failure.
/// </summary>
public class CalendarAuthException(string message) : Exception(message);

/// <summary>
/// Abstraction over a calendar provider's OAuth + event-listing REST API. One implementation
/// per <see cref="CalendarProvider"/>, resolved via <see cref="ICalendarProviderClientFactory"/>.
/// </summary>
public interface ICalendarProviderClient
{
    CalendarProvider Provider { get; }

    string BuildAuthorizationUrl(string state, string redirectUri);

    Task<ExternalTokenResponse> ExchangeAuthorizationCodeAsync(string code, string redirectUri, CancellationToken cancellationToken);

    Task<string> GetAccountEmailAsync(string accessToken, CancellationToken cancellationToken);

    /// <exception cref="CalendarAuthException">The refresh token was rejected by the provider.</exception>
    Task<IReadOnlyList<ProviderEvent>> GetEventsAsync(string refreshToken, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken);
}

public interface ICalendarProviderClientFactory
{
    ICalendarProviderClient Get(CalendarProvider provider);
}
