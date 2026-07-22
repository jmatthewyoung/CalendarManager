using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Domain.Enums;
using Microsoft.Extensions.Options;

namespace CalendarManager.Infrastructure.CalendarProviders;

public class GoogleCalendarClient : ICalendarProviderClient
{
    private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string UserInfoEndpoint = "https://openidconnect.googleapis.com/v1/userinfo";
    private const string EventsEndpoint = "https://www.googleapis.com/calendar/v3/calendars/primary/events";
    private const string Scope = "https://www.googleapis.com/auth/calendar.readonly openid email";

    private readonly HttpClient _httpClient;
    private readonly GoogleCalendarOptions _options;

    public GoogleCalendarClient(HttpClient httpClient, IOptions<GoogleCalendarOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public CalendarProvider Provider => CalendarProvider.Google;

    public string BuildAuthorizationUrl(string state, string redirectUri)
    {
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = Scope,
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["state"] = state
        };

        return $"{AuthorizationEndpoint}{QueryString.Build(query)}";
    }

    public async Task<ExternalTokenResponse> ExchangeAuthorizationCodeAsync(string code, string redirectUri, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsync(TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        }), cancellationToken);

        await ThrowIfAuthFailureAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Google token endpoint returned an empty response.");

        return new ExternalTokenResponse(payload.AccessToken, payload.RefreshToken, DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn));
    }

    public async Task<string> GetAccountEmailAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UserInfoEndpoint);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        await ThrowIfAuthFailureAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<GoogleUserInfoResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Google userinfo endpoint returned an empty response.");

        return payload.Email;
    }

    public async Task<IReadOnlyList<ProviderEvent>> GetEventsAsync(string refreshToken, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken)
    {
        var accessToken = await RefreshAccessTokenAsync(refreshToken, cancellationToken);

        var query = new Dictionary<string, string?>
        {
            ["timeMin"] = start.ToString("O"),
            ["timeMax"] = end.ToString("O"),
            ["singleEvents"] = "true",
            ["maxResults"] = "2500"
        };

        var events = new List<ProviderEvent>();
        string? pageToken = null;

        do
        {
            var url = $"{EventsEndpoint}{QueryString.Build(pageToken is null ? query : new Dictionary<string, string?>(query) { ["pageToken"] = pageToken })}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            await ThrowIfAuthFailureAsync(response, cancellationToken);

            var payload = await response.Content.ReadFromJsonAsync<GoogleEventsResponse>(cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("Google events endpoint returned an empty response.");

            foreach (var item in payload.Items ?? [])
            {
                var (eventStart, isAllDay) = ParseDateTime(item.Start);
                var (eventEnd, _) = ParseDateTime(item.End);

                var attendees = (item.Attendees ?? [])
                    .Where(a => !string.IsNullOrEmpty(a.Email))
                    .Select(a => new ProviderAttendee(a.Email!, a.DisplayName, MapResponseStatus(a.ResponseStatus)))
                    .ToList();

                events.Add(new ProviderEvent(
                    item.Id,
                    item.Summary ?? "(No title)",
                    eventStart,
                    eventEnd,
                    isAllDay,
                    item.Organizer?.Email,
                    item.Organizer?.DisplayName,
                    attendees));
            }

            pageToken = payload.NextPageToken;
        } while (pageToken is not null);

        return events;
    }

    private async Task<string> RefreshAccessTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsync(TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token"
        }), cancellationToken);

        await ThrowIfAuthFailureAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Google token endpoint returned an empty response.");

        return payload.AccessToken;
    }

    private static AttendeeResponseStatus MapResponseStatus(string? status) => status switch
    {
        "accepted" => AttendeeResponseStatus.Accepted,
        "declined" => AttendeeResponseStatus.Declined,
        "tentative" => AttendeeResponseStatus.Tentative,
        _ => AttendeeResponseStatus.NeedsAction
    };

    private static (DateTimeOffset Value, bool IsAllDay) ParseDateTime(GoogleEventDateTime? dateTime)
    {
        if (dateTime is null)
        {
            return (DateTimeOffset.UtcNow, false);
        }

        if (dateTime.DateTimeValue is { } dt)
        {
            return (dt, false);
        }

        if (dateTime.Date is { } date && DateOnly.TryParse(date, out var parsedDate))
        {
            return (new DateTimeOffset(parsedDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), true);
        }

        return (DateTimeOffset.UtcNow, false);
    }

    private static async Task ThrowIfAuthFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new CalendarAuthException($"Google API call failed with status {(int)response.StatusCode}.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Google API call failed with status {(int)response.StatusCode}: {body}");
        }
    }

    private class GoogleTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    private class GoogleUserInfoResponse
    {
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;
    }

    private class GoogleEventsResponse
    {
        [JsonPropertyName("items")]
        public List<GoogleEvent>? Items { get; set; }

        [JsonPropertyName("nextPageToken")]
        public string? NextPageToken { get; set; }
    }

    private class GoogleEvent
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("start")]
        public GoogleEventDateTime? Start { get; set; }

        [JsonPropertyName("end")]
        public GoogleEventDateTime? End { get; set; }

        [JsonPropertyName("organizer")]
        public GoogleEventPerson? Organizer { get; set; }

        [JsonPropertyName("attendees")]
        public List<GoogleEventAttendee>? Attendees { get; set; }
    }

    private class GoogleEventDateTime
    {
        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("dateTime")]
        public DateTimeOffset? DateTimeValue { get; set; }
    }

    private class GoogleEventPerson
    {
        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }
    }

    private class GoogleEventAttendee
    {
        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("responseStatus")]
        public string? ResponseStatus { get; set; }
    }
}
