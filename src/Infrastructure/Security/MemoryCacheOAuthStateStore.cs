using CalendarManager.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace CalendarManager.Infrastructure.Security;

/// <summary>
/// Stores single-use OAuth CSRF state server-side, keyed by an opaque token handed to the client.
/// In-memory is sufficient since the connect flow round-trips through the same App Service instance
/// within its short lifetime; a multi-instance deployment would need a distributed cache instead.
/// </summary>
public class MemoryCacheOAuthStateStore : IOAuthStateStore
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly IMemoryCache _cache;

    public MemoryCacheOAuthStateStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public string Create(string userId, Domain.Enums.CalendarProvider provider)
    {
        var token = Guid.NewGuid().ToString("N");

        _cache.Set(CacheKey(token), new OAuthState(userId, provider), Lifetime);

        return token;
    }

    public OAuthState? Validate(string state)
    {
        var key = CacheKey(state);

        if (!_cache.TryGetValue(key, out OAuthState? value))
        {
            return null;
        }

        _cache.Remove(key);

        return value;
    }

    private static string CacheKey(string token) => $"oauth-state:{token}";
}
