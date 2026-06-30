using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlassLauncher.Services;

namespace GlassLauncher.Core;

// Авторизация через Ely.by по OAuth2 Authorization Code + PKCE.
// Документация: https://docs.ely.by/en/oauth.html
public class ElyByAuthService
{
    // === Конфигурация OAuth-приложения Ely.by ===
    // Зарегистрируйте приложение на https://account.ely.by/dev/applications
    // и вставьте свой clientId. redirectUri должен совпадать с настройками приложения.
    public const string ClientId = "sepioled-launcher";
    public const string RedirectUri = "http://localhost:53682/callback";
    private const string AuthEndpoint = "https://account.ely.by/oauth2/v1";
    private const string TokenEndpoint = "https://account.ely.by/api/oauth2/v1/token";
    private const string AccountInfo = "https://account.ely.by/api/account/v1/info";

    private readonly ApiClient _api;
    public ElyByAuthService(ApiClient api) => _api = api;

    private string? _codeVerifier; // PKCE verifier текущей сессии

    // Шаг 1: строим URL авторизации с code_challenge (S256).
    public string BuildAuthUrl()
    {
        _codeVerifier = GenerateCodeVerifier();
        var challenge = GenerateCodeChallenge(_codeVerifier);
        var scopes = "account_info minecraft_server_session offline_access";
        return $"{AuthEndpoint}?client_id={ClientId}" +
               $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
               $"&response_type=code" +
               $"&scope={Uri.EscapeDataString(scopes)}" +
               $"&code_challenge={challenge}&code_challenge_method=S256" +
               $"&prompt=select_account";
    }

    // Шаг 2: меняем authorization code на токены.
    public async Task<ElyAccount> ExchangeCodeAsync(string code, CancellationToken ct = default)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"]     = ClientId,
            ["grant_type"]    = "authorization_code",
            ["code"]          = code,
            ["redirect_uri"]  = RedirectUri,
            ["code_verifier"] = _codeVerifier ?? ""
        });

        using var resp = await _api.Raw.PostAsync(TokenEndpoint, form, ct);
        resp.EnsureSuccessStatusCode();
        var token = await resp.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct)
                    ?? throw new Exception("Не удалось получить токен Ely.by");

        return await LoadAccountAsync(token.access_token, token.refresh_token, ct);
    }

    // Шаг 3 (опц.): обновление по refresh_token для авто-логина.
    public async Task<ElyAccount> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"]     = ClientId,
            ["grant_type"]    = "refresh_token",
            ["refresh_token"] = refreshToken
        });
        using var resp = await _api.Raw.PostAsync(TokenEndpoint, form, ct);
        resp.EnsureSuccessStatusCode();
        var token = await resp.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct)!;
        return await LoadAccountAsync(token!.access_token, token.refresh_token ?? refreshToken, ct);
    }

    // Загружает данные аккаунта (ник, uuid) и формирует URL скина/аватара.
    private async Task<ElyAccount> LoadAccountAsync(string accessToken, string? refresh, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, AccountInfo);
        req.Headers.Add("Authorization", $"Bearer {accessToken}");
        using var resp = await _api.Raw.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        var info = await resp.Content.ReadFromJsonAsync<AccountInfoResponse>(cancellationToken: ct)!;

        return new ElyAccount
        {
            Id = info!.id.ToString(),
            Username = info.username,
            Uuid = info.uuid,
            // Рендер головы скина через сервис Ely.by
            SkinUrl = $"http://skinsystem.ely.by/skins/{info.username}.png",
            AccessToken = accessToken,
            RefreshToken = refresh
        };
    }

    // ===== PKCE =====
    private static string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64Url(bytes);
    }

    private static string GenerateCodeChallenge(string verifier)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.ASCII.GetBytes(verifier));
        return Base64Url(hash);
    }

    private static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    // DTO
    private record TokenResponse(string access_token, string? refresh_token, int expires_in);
    private record AccountInfoResponse(long id, string username, string uuid, string? email);
}