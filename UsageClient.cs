using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace CodexQuota;

public sealed record UsageWindow(double RemainingPercent, DateTimeOffset? ResetAt);
public sealed record UsageSnapshot(UsageWindow? FiveHour, UsageWindow? Week);
public sealed class NotAuthenticatedException : Exception { }

public sealed class UsageClient : IDisposable
{
    private readonly HttpClient _http = new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All });
    private const string Endpoint = "https://chatgpt.com/backend-api/wham/usage";
    private Func<Task<string>>? _browserFetcher;

    public void SetBrowserFetcher(Func<Task<string>> fetcher) => _browserFetcher = fetcher;

    public void SetBearerCredentials(string accessToken, string? accountId)
    {
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        _http.DefaultRequestHeaders.Remove("ChatGPT-Account-Id");
        if (!string.IsNullOrWhiteSpace(accountId))
            _http.DefaultRequestHeaders.TryAddWithoutValidation("ChatGPT-Account-Id", accountId);
        _http.DefaultRequestHeaders.Remove("originator");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("originator", "Codex Desktop");
        _http.DefaultRequestHeaders.Accept.Clear();
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public void SetCookieHeader(string cookieHeader)
    {
        _http.DefaultRequestHeaders.Remove("Cookie");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookieHeader);
    }

    public void ClearAuthentication()
    {
        _http.DefaultRequestHeaders.Authorization = null;
        _http.DefaultRequestHeaders.Remove("ChatGPT-Account-Id");
        _http.DefaultRequestHeaders.Remove("Cookie");
        _browserFetcher = null;
    }

    public async Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken = default)
    {
        if (_http.DefaultRequestHeaders.Authorization is null && _browserFetcher is not null)
        {
            var browserJson = await _browserFetcher();
            using var browserDoc = JsonDocument.Parse(browserJson);
            return Parse(browserDoc.RootElement);
        }

        using var response = await _http.GetAsync(Endpoint, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new NotAuthenticatedException();
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return Parse(doc.RootElement);
    }

    private static UsageSnapshot Parse(JsonElement root)
    {
        UsageWindow? five = null, week = null;
        Visit(root, ref five, ref week);
        return new(five, week);
    }

    private static void Visit(JsonElement e, ref UsageWindow? five, ref UsageWindow? week)
    {
        if (e.ValueKind != JsonValueKind.Object) return;
        foreach (var p in e.EnumerateObject())
        {
            var name = p.Name.ToLowerInvariant();
            if (p.Value.ValueKind == JsonValueKind.Object)
            {
                var w = ParseWindow(p.Value);
                if (w is not null && five is null && (name.Contains("5") || name.Contains("session") || name.Contains("hour") || name.Contains("primary"))) five = w;
                if (w is not null && week is null && (name.Contains("week") || name.Contains("7d") || name.Contains("weekly") || name.Contains("secondary"))) week = w;
                Visit(p.Value, ref five, ref week);
            }
        }
    }

    private static UsageWindow? ParseWindow(JsonElement e)
    {
        double? used = Number(e, "used_percent") ?? Number(e, "usedPercent") ?? Number(e, "percent");
        var reset = Date(e, "reset_at") ?? Date(e, "resetAt") ?? Date(e, "resets_at");
        return used is null && reset is null ? null : new(Math.Clamp(100 - (used ?? 0), 0, 100), reset);
    }
    private static double? Number(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var n) ? n : null;
    private static DateTimeOffset? Date(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(v.GetString(), out var textDate)) return textDate;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var unix))
            return DateTimeOffset.FromUnixTimeSeconds(unix > 10_000_000_000 ? unix / 1000 : unix);
        return null;
    }
    public void Dispose() => _http.Dispose();
}
