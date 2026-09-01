using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CodexQuota;

public sealed record OAuthCredentials(string AccessToken, string? AccountId, string? RefreshToken = null);

public sealed class OAuthLoginService : IDisposable
{
    private const string ClientId = "app_EMoamEEZ73f0CkXaXp7hrann";
    private const string RedirectUri = "http://localhost:1455/auth/callback";
    private readonly HttpClient _http = new();
    private string? _verifier;
    private string? _state;

    public Uri CreateAuthorizationUri()
    {
        _verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        _state = Base64Url(RandomNumberGenerator.GetBytes(24));
        var challenge = Base64Url(SHA256.HashData(Encoding.UTF8.GetBytes(_verifier)));
        var query = string.Join("&", new Dictionary<string, string>
        {
            ["response_type"] = "code", ["client_id"] = ClientId, ["redirect_uri"] = RedirectUri,
            ["scope"] = "openid profile email offline_access", ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256", ["id_token_add_organizations"] = "true",
            ["codex_cli_simplified_flow"] = "true", ["state"] = _state, ["originator"] = "codex_desktop"
        }.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
        return new Uri($"https://auth.openai.com/oauth/authorize?{query}");
    }

    public async Task<OAuthCredentials> CompleteAsync(Uri callback)
    {
        var values = ParseQuery(callback.Query);
        if (!values.TryGetValue("state", out var returnedState) || returnedState != _state)
            throw new InvalidOperationException("Ungültiger OAuth-Status.");
        if (values.TryGetValue("error", out var error))
            throw new InvalidOperationException($"Anmeldung abgebrochen: {error}");
        if (!values.TryGetValue("code", out var code) || _verifier is null)
            throw new InvalidOperationException("Kein Anmeldecode erhalten.");

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["client_id"] = ClientId,
            ["code"] = code, ["redirect_uri"] = RedirectUri, ["code_verifier"] = _verifier
        });
        using var response = await _http.PostAsync("https://auth.openai.com/oauth/token", content);
        var body = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(body);
        var accessToken = json.RootElement.GetProperty("access_token").GetString();
        var idToken = json.RootElement.TryGetProperty("id_token", out var id) ? id.GetString() : null;
        if (string.IsNullOrWhiteSpace(accessToken)) throw new InvalidOperationException("Kein Zugriffstoken erhalten.");
        var refreshToken = json.RootElement.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() : null;
        return new(accessToken, ReadAccountId(idToken), refreshToken);
    }

    public static async Task<OAuthCredentials> RefreshAsync(OAuthCredentials current)
    {
        if (string.IsNullOrWhiteSpace(current.RefreshToken)) return current;
        using var http = new HttpClient();
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token", ["client_id"] = ClientId, ["refresh_token"] = current.RefreshToken
        });
        using var response = await http.PostAsync("https://auth.openai.com/oauth/token", content);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var access = json.RootElement.GetProperty("access_token").GetString() ?? throw new InvalidOperationException("Kein Zugriffstoken erhalten.");
        var refresh = json.RootElement.TryGetProperty("refresh_token", out var token) ? token.GetString() : current.RefreshToken;
        var idToken = json.RootElement.TryGetProperty("id_token", out var id) ? id.GetString() : null;
        return new(access, ReadAccountId(idToken) ?? current.AccountId, refresh);
    }

    private static string? ReadAccountId(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var parts = token.Split('.');
        if (parts.Length < 2) return null;
        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        using var doc = JsonDocument.Parse(Convert.FromBase64String(payload));
        if (doc.RootElement.TryGetProperty("https://api.openai.com/auth", out var auth) && auth.ValueKind == JsonValueKind.Object && auth.TryGetProperty("chatgpt_account_id", out var nested)) return nested.GetString();
        return doc.RootElement.TryGetProperty("chatgpt_account_id", out var direct) ? direct.GetString() : null;
    }

    private static Dictionary<string, string> ParseQuery(string query) => query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('=', 2)).Where(p => p.Length == 2).ToDictionary(p => WebUtility.UrlDecode(p[0]), p => WebUtility.UrlDecode(p[1]));
    private static string Base64Url(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public void Dispose() => _http.Dispose();
}
