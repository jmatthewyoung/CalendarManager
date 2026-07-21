using System.Net;
using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Application.UnitTests.TestHelpers;
using CalendarManager.Domain.Enums;
using CalendarManager.Infrastructure.CalendarProviders;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Shouldly;

namespace CalendarManager.Application.UnitTests.CalendarProviders;

public class OutlookCalendarClientTests
{
    private static OutlookCalendarClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var httpClient = new HttpClient(new FakeHttpMessageHandler(handler));
        var options = Options.Create(new OutlookCalendarOptions { ClientId = "client-id", ClientSecret = "client-secret", TenantId = "common" });
        return new OutlookCalendarClient(httpClient, options);
    }

    [Test]
    public void Provider_IsOutlook()
    {
        var client = CreateClient(_ => throw new InvalidOperationException("no HTTP call expected"));

        client.Provider.ShouldBe(CalendarProvider.Outlook);
    }

    [Test]
    public void BuildAuthorizationUrl_UsesTenantAndState()
    {
        var client = CreateClient(_ => throw new InvalidOperationException("no HTTP call expected"));

        var url = client.BuildAuthorizationUrl("the-state", "https://app.example.com/callback");

        url.ShouldStartWith("https://login.microsoftonline.com/common/oauth2/v2.0/authorize?");
        url.ShouldContain("state=the-state");
    }

    [Test]
    public async Task GetAccountEmailAsync_PrefersMailOverUserPrincipalName()
    {
        var client = CreateClient(request =>
        {
            request.RequestUri!.ToString().ShouldBe("https://graph.microsoft.com/v1.0/me");
            return FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK,
                """{"mail":"someone@example.com","userPrincipalName":"someone@tenant.onmicrosoft.com"}""");
        });

        var email = await client.GetAccountEmailAsync("access-token", CancellationToken.None);

        email.ShouldBe("someone@example.com");
    }

    [Test]
    public async Task GetAccountEmailAsync_FallsBackToUserPrincipalNameWhenMailIsNull()
    {
        var client = CreateClient(_ => FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK,
            """{"mail":null,"userPrincipalName":"someone@tenant.onmicrosoft.com"}"""));

        var email = await client.GetAccountEmailAsync("access-token", CancellationToken.None);

        email.ShouldBe("someone@tenant.onmicrosoft.com");
    }

    [Test]
    public async Task GetEventsAsync_RefreshesTokenThenParsesEvents()
    {
        var client = CreateClient(request =>
        {
            if (request.RequestUri!.ToString().StartsWith("https://login.microsoftonline.com/"))
            {
                return FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK, """{"access_token":"access-1","refresh_token":"","expires_in":3600}""");
            }

            if (request.RequestUri.ToString().StartsWith("https://graph.microsoft.com/v1.0/me/calendarView"))
            {
                request.Headers.GetValues("Prefer").ShouldContain("outlook.timezone=\"UTC\"");
                return FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK, """
                    {
                      "value": [
                        { "id": "ev-1", "subject": "Standup", "isAllDay": false, "start": { "dateTime": "2026-07-21T09:00:00.0000000" }, "end": { "dateTime": "2026-07-21T09:30:00.0000000" } }
                      ]
                    }
                    """);
            }

            throw new InvalidOperationException($"Unexpected request to {request.RequestUri}");
        });

        var events = await client.GetEventsAsync("refresh-token", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), CancellationToken.None);

        events.Count.ShouldBe(1);
        events[0].ExternalEventId.ShouldBe("ev-1");
        events[0].Title.ShouldBe("Standup");
        events[0].IsAllDay.ShouldBeFalse();
    }

    [Test]
    public async Task GetEventsAsync_FollowsODataNextLinkAcrossPages()
    {
        var callCount = 0;
        var client = CreateClient(request =>
        {
            if (request.RequestUri!.ToString().StartsWith("https://login.microsoftonline.com/"))
            {
                return FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK, """{"access_token":"access-1","refresh_token":"","expires_in":3600}""");
            }

            callCount++;
            if (callCount == 1)
            {
                return FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK, """
                    {
                      "value": [{ "id": "ev-1", "subject": "Page 1", "isAllDay": false, "start": { "dateTime": "2026-07-21T09:00:00.0000000" }, "end": { "dateTime": "2026-07-21T09:30:00.0000000" } }],
                      "@odata.nextLink": "https://graph.microsoft.com/v1.0/me/calendarView?$skip=1"
                    }
                    """);
            }

            request.RequestUri.ToString().ShouldBe("https://graph.microsoft.com/v1.0/me/calendarView?$skip=1");
            return FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK, """
                { "value": [{ "id": "ev-2", "subject": "Page 2", "isAllDay": false, "start": { "dateTime": "2026-07-21T10:00:00.0000000" }, "end": { "dateTime": "2026-07-21T10:30:00.0000000" } }] }
                """);
        });

        var events = await client.GetEventsAsync("refresh-token", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), CancellationToken.None);

        events.Count.ShouldBe(2);
        events.Select(e => e.ExternalEventId).ShouldBe(["ev-1", "ev-2"]);
    }

    [Test]
    public void GetAccountEmailAsync_On403_ThrowsCalendarAuthException()
    {
        var client = CreateClient(_ => FakeHttpMessageHandler.JsonResponse(HttpStatusCode.Forbidden, """{"error":"forbidden"}"""));

        Should.ThrowAsync<CalendarAuthException>(() =>
            client.GetAccountEmailAsync("access-token", CancellationToken.None));
    }
}
