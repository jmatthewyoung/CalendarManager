using System.Net;
using System.Text.Json;
using CalendarManager.Application.Common.Interfaces;
using Microsoft.Extensions.Options;
using WebPush;

namespace CalendarManager.Infrastructure.Push;

public class WebPushNotificationService : IPushNotificationService
{
    private readonly WebPushClient _client;
    private readonly WebPushOptions _options;

    public WebPushNotificationService(IOptions<WebPushOptions> options)
    {
        _options = options.Value;
        _client = new WebPushClient();
    }

    public string PublicKey => _options.PublicKey;

    public async Task SendAsync(Domain.Entities.PushSubscription subscription, string title, string body, CancellationToken cancellationToken)
    {
        var vapidDetails = new VapidDetails(_options.Subject, _options.PublicKey, _options.PrivateKey);
        var pushSubscription = new global::WebPush.PushSubscription(subscription.Endpoint, subscription.P256dhKey, subscription.AuthKey);

        // Angular's service worker only renders a notification when the payload is shaped
        // { notification: { ... } } — a flat { title, body } object is silently dropped.
        var payload = JsonSerializer.Serialize(new
        {
            notification = new
            {
                title,
                body,
                icon = "icons/icon-192x192.png",
                data = new { onActionClick = new { @default = new { operation = "openWindow", url = "/" } } }
            }
        });

        try
        {
            await _client.SendNotificationAsync(pushSubscription, payload, vapidDetails, cancellationToken: cancellationToken);
        }
        catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            throw new PushSubscriptionExpiredException($"Push subscription for endpoint '{subscription.Endpoint}' is no longer valid.");
        }
    }
}
