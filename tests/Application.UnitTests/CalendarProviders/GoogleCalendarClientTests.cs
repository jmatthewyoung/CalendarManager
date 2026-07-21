using System.Net;
using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Application.UnitTests.TestHelpers;
using CalendarManager.Domain.Enums;
using CalendarManager.Infrastructure.CalendarProviders;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Shouldly;

namespace CalendarManager.Application.UnitTests.CalendarProviders;

public class GoogleCalendarClientTests
{
    private static GoogleCalendarClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var httpClient = new HttpClient(new FakeHttpMessageHandler(handler));
        var options = Options.Create(new GoogleCalendarOptions { ClientId = "client-id", ClientSecret = "client-secret" });
        return new GoogleCalendarClient(httpClient, options);
    }

    [Test]
    public void Provider_IsGoogle()
    {
        var client = CreateClient(_ => throw new InvalidOperationException("no HTTP call expected"));

        client.Provider.ShouldBe(CalendarProvider.Google);
    }

    [Test]
    public void BuildAuthorizationUrl_IncludesStateAndRedirectUri()
    {
        var client = CreateClient(_ => throw new InvalidOperationException("no HTTP call expected"));

        var url = client.BuildAuthorizationUrl("the-state", "https://app.example.com/callback");

        url.ShouldStartWith("https://accounts.google.com/o/oauth2/v2/auth?");
        url.ShouldContain("state=the-state");
        url.ShouldContain(Uri.EscapeDataString("https://app.example.com/callback"));
    }

    [Test]
    public async Task ExchangeAuthorizationCodeAsync_ReturnsTokensFromResponse()
    {
        var client = CreateClient(request =>
        {
            request.RequestUri!.ToString().ShouldBe("https://oauth2.googleapis.com/token");
            return FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK,
                """{"access_token":"access-1","refresh_token":"refresh-1","expires_in":3600}""");
        });

        var result = await client.ExchangeAuthorizationCodeAsync("auth-code", "https://app/callback", CancellationToken.None);

        result.AccessToken.ShouldBe("access-1");
        result.RefreshToken.ShouldBe("refresh-1");
    }

    [Test]
    public async Task GetAccountEmailAsync_ReturnsEmailFromUserInfoEndpoint()
    {
        var client = CreateClient(request =>
        {
            request.RequestUri!.ToString().ShouldBe("https://openidconnect.googleapis.com/v1/userinfo");
            request.Headers.Authorization!.Parameter.ShouldBe("access-token");
            return FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK, """{"email":"someone@example.com"}""");
        });

        var email = await client.GetAccountEmailAsync("access-token", CancellationToken.None);

        email.ShouldBe("someone@example.com");
    }

    [Test]
    public async Task GetEventsAsync_RefreshesTokenThenParsesTimedAndAllDayEvents()
    {
        var client = CreateClient(request =>
        {
            if (request.RequestUri!.ToString() == "https://oauth2.googleapis.com/token")
            {
                return FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK, """{"access_token":"access-1","refresh_token":"","expires_in":3600}""");
            }

            if (request.RequestUri.ToString().StartsWith("https://www.googleapis.com/calendar/v3/calendars/primary/events"))
            {
                return FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK, """
                    {
                      "items": [
                        { "id": "ev-1", "summary": "Standup", "start": { "dateTime": "2026-07-21T09:00:00Z" }, "end": { "dateTime": "2026-07-21T09:30:00Z" } },
                        { "id": "ev-2", "summary": "Conference", "start": { "date": "2026-07-22" }, "end": { "date": "2026-07-23" } }
                      ]
                    }
                    """);
            }

            throw new InvalidOperationException($"Unexpected request to {request.RequestUri}");
        });

        var events = await client.GetEventsAsync("refresh-token", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), CancellationToken.None);

        events.Count.ShouldBe(2);
        var timed = events.Single(e => e.ExternalEventId == "ev-1");
        timed.Title.ShouldBe("Standup");
        timed.IsAllDay.ShouldBeFalse();

        var allDay = events.Single(e => e.ExternalEventId == "ev-2");
        allDay.Title.ShouldBe("Conference");
        allDay.IsAllDay.ShouldBeTrue();
    }

    [Test]
    public async Task GetEventsAsync_FollowsPageTokenAcrossMultiplePages()
    {
        var pageCount = 0;
        var client = CreateClient(request =>
        {
            if (request.RequestUri!.ToString() == "https://oauth2.googleapis.com/token")
            {
                return FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK, """{"access_token":"access-1","refresh_token":"","expires_in":3600}""");
            }

            pageCount++;
            if (pageCount == 1)
            {
                return FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK, """
                    { "items": [{ "id": "ev-1", "summary": "Page 1", "start": { "dateTime": "2026-07-21T09:00:00Z" }, "end": { "dateTime": "2026-07-21T09:30:00Z" } }], "nextPageToken": "token-2" }
                    """);
            }

            request.RequestUri.ToString().ShouldContain("pageToken=token-2");
            return FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK, """
                { "items": [{ "id": "ev-2", "summary": "Page 2", "start": { "dateTime": "2026-07-21T10:00:00Z" }, "end": { "dateTime": "2026-07-21T10:30:00Z" } }] }
                """);
        });

        var events = await client.GetEventsAsync("refresh-token", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), CancellationToken.None);

        events.Count.ShouldBe(2);
        events.Select(e => e.ExternalEventId).ShouldBe(["ev-1", "ev-2"]);
    }

    [Test]
    public void ExchangeAuthorizationCodeAsync_On401_ThrowsCalendarAuthException()
    {
        var client = CreateClient(_ => FakeHttpMessageHandler.JsonResponse(HttpStatusCode.Unauthorized, """{"error":"invalid_grant"}"""));

        Should.ThrowAsync<CalendarAuthException>(() =>
            client.ExchangeAuthorizationCodeAsync("bad-code", "https://app/callback", CancellationToken.None));
    }
}
