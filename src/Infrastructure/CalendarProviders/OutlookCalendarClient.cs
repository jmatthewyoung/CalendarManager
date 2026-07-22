using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Domain.Enums;
using Microsoft.Extensions.Options;

namespace CalendarManager.Infrastructure.CalendarProviders;

public class OutlookCalendarClient : ICalendarProviderClient
{
    private const string GraphMeEndpoint = "https://graph.microsoft.com/v1.0/me";
    private const string GraphCalendarViewEndpoint = "https://graph.microsoft.com/v1.0/me/calendarView";
    private const string Scope = "offline_access openid email User.Read Calendars.Read";

    private readonly HttpClient _httpClient;
    private readonly OutlookCalendarOptions _options;

    public OutlookCalendarClient(HttpClient httpClient, IOptions<OutlookCalendarOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public CalendarProvider Provider => CalendarProvider.Outlook;

    private string AuthorizationEndpoint => $"https://login.microsoftonline.com/{_options.TenantId}/oauth2/v2.0/authorize";

    private string TokenEndpoint => $"https://login.microsoftonline.com/{_options.TenantId}/oauth2/v2.0/token";

    public string BuildAuthorizationUrl(string state, string redirectUri)
    {
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["response_mode"] = "query",
            ["scope"] = Scope,
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
            ["grant_type"] = "authorization_code",
            ["scope"] = Scope
        }), cancellationToken);

        await ThrowIfAuthFailureAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<GraphTokenResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Microsoft identity token endpoint returned an empty response.");

        return new ExternalTokenResponse(payload.AccessToken, payload.RefreshToken, DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn));
    }

    public async Task<string> GetAccountEmailAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, GraphMeEndpoint);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        await ThrowIfAuthFailureAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<GraphUserResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Microsoft Graph /me endpoint returned an empty response.");

        return payload.Mail ?? payload.UserPrincipalName ?? throw new InvalidOperationException("Microsoft Graph account has no email address.");
    }

    public async Task<IReadOnlyList<ProviderEvent>> GetEventsAsync(string refreshToken, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken)
    {
        var accessToken = await RefreshAccessTokenAsync(refreshToken, cancellationToken);

        var query = new Dictionary<string, string?>
        {
            ["startDateTime"] = start.UtcDateTime.ToString("O"),
            ["endDateTime"] = end.UtcDateTime.ToString("O"),
            ["$top"] = "100"
        };

        var events = new List<ProviderEvent>();
        string? nextLink = $"{GraphCalendarViewEndpoint}{QueryString.Build(query)}";

        while (nextLink is not null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, nextLink);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Add("Prefer", "outlook.timezone=\"UTC\"");

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            await ThrowIfAuthFailureAsync(response, cancellationToken);

            var payload = await response.Content.ReadFromJsonAsync<GraphEventsResponse>(cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("Microsoft Graph calendarView endpoint returned an empty response.");

            foreach (var item in payload.Value ?? [])
            {
                var attendees = (item.Attendees ?? [])
                    .Where(a => !string.IsNullOrEmpty(a.EmailAddress?.Address))
                    .Select(a => new ProviderAttendee(a.EmailAddress!.Address!, a.EmailAddress.Name, MapResponseStatus(a.Status?.Response)))
                    .ToList();

                events.Add(new ProviderEvent(
                    item.Id,
                    item.Subject ?? "(No title)",
                    DateTimeOffset.Parse(item.Start!.DateTime + "Z"),
                    DateTimeOffset.Parse(item.End!.DateTime + "Z"),
                    item.IsAllDay,
                    item.Organizer?.EmailAddress?.Address,
                    item.Organizer?.EmailAddress?.Name,
                    attendees));
            }

            nextLink = payload.NextLink;
        }

        return events;
    }

    private async Task<string> RefreshAccessTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsync(TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token",
            ["scope"] = Scope
        }), cancellationToken);

        await ThrowIfAuthFailureAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<GraphTokenResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Microsoft identity token endpoint returned an empty response.");

        return payload.AccessToken;
    }

    private static AttendeeResponseStatus MapResponseStatus(string? response) => response switch
    {
        "accepted" or "organizer" => AttendeeResponseStatus.Accepted,
        "declined" => AttendeeResponseStatus.Declined,
        "tentativelyAccepted" => AttendeeResponseStatus.Tentative,
        _ => AttendeeResponseStatus.NeedsAction
    };

    private static async Task ThrowIfAuthFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new CalendarAuthException($"Microsoft Graph API call failed with status {(int)response.StatusCode}.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Microsoft Graph API call failed with status {(int)response.StatusCode}: {body}");
        }
    }

    private class GraphTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    private class GraphUserResponse
    {
        [JsonPropertyName("mail")]
        public string? Mail { get; set; }

        [JsonPropertyName("userPrincipalName")]
        public string? UserPrincipalName { get; set; }
    }

    private class GraphEventsResponse
    {
        [JsonPropertyName("value")]
        public List<GraphEvent>? Value { get; set; }

        [JsonPropertyName("@odata.nextLink")]
        public string? NextLink { get; set; }
    }

    private class GraphEvent
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("subject")]
        public string? Subject { get; set; }

        [JsonPropertyName("isAllDay")]
        public bool IsAllDay { get; set; }

        [JsonPropertyName("start")]
        public GraphDateTimeTimeZone? Start { get; set; }

        [JsonPropertyName("end")]
        public GraphDateTimeTimeZone? End { get; set; }

        [JsonPropertyName("organizer")]
        public GraphRecipient? Organizer { get; set; }

        [JsonPropertyName("attendees")]
        public List<GraphAttendee>? Attendees { get; set; }
    }

    private class GraphDateTimeTimeZone
    {
        [JsonPropertyName("dateTime")]
        public string DateTime { get; set; } = string.Empty;
    }

    private class GraphRecipient
    {
        [JsonPropertyName("emailAddress")]
        public GraphEmailAddress? EmailAddress { get; set; }
    }

    private class GraphAttendee
    {
        [JsonPropertyName("emailAddress")]
        public GraphEmailAddress? EmailAddress { get; set; }

        [JsonPropertyName("status")]
        public GraphResponseStatus? Status { get; set; }
    }

    private class GraphEmailAddress
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("address")]
        public string? Address { get; set; }
    }

    private class GraphResponseStatus
    {
        [JsonPropertyName("response")]
        public string? Response { get; set; }
    }
}
