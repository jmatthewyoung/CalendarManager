using System.Web;

namespace CalendarManager.Infrastructure.CalendarProviders;

internal static class QueryString
{
    public static string Build(IReadOnlyDictionary<string, string?> parameters)
    {
        var pairs = parameters
            .Where(p => p.Value is not null)
            .Select(p => $"{HttpUtility.UrlEncode(p.Key)}={HttpUtility.UrlEncode(p.Value)}");

        return $"?{string.Join('&', pairs)}";
    }
}
