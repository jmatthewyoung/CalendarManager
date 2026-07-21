using CalendarManager.Domain.Entities;

namespace CalendarManager.Application.Common.Interfaces;

/// <summary>
/// Thrown when a push send fails because the browser subscription is no longer valid
/// (unsubscribed, expired, or the endpoint was revoked), so the caller knows to prune it.
/// </summary>
public class PushSubscriptionExpiredException(string message) : Exception(message);

public interface IPushNotificationService
{
    /// <summary>The VAPID public key, exposed to the SPA so it can create a push subscription.</summary>
    string PublicKey { get; }

    /// <exception cref="PushSubscriptionExpiredException">The subscription is no longer valid.</exception>
    Task SendAsync(PushSubscription subscription, string title, string body, CancellationToken cancellationToken);
}
