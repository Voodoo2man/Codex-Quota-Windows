using System.Net;
using System.Text.Json;

namespace CodexQuota;

public sealed record UsageWindow(double RemainingPercent, DateTimeOffset? ResetAt);
public sealed record UsageSnapshot(UsageWindow? FiveHour, UsageWindow? Week);
public sealed class NotAuthenticatedException : Exception { }

public sealed class UsageClient : IDisposable
{
    private readonly HttpClient _http = new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All });
    private const string Endpoint = "https://chatgpt.com/backend-api/wham/usage";

    public void SetCookieHeader(string cookieHeader)
    {
        _http.DefaultRequestHeaders.Remove("Cookie");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookieHeader);
    }

    public async Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken = default)
    {
        // MVP: the WebView2 login will provide cookies in the next step. Keeping this
        // provider isolated makes the undocumented endpoint easy to replace.
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
                if (w is not null && week is null && (name.Contains("week") || name.Contains("7d") || name.Contains("weekly"))) week = w;
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
    private static DateTimeOffset? Date(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(v.GetString(), out var d) ? d : null;
    public void Dispose() => _http.Dispose();
}
