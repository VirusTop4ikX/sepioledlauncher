using System.IO;
using System.Text.Json;
using GlassLauncher.Services;

namespace GlassLauncher.Core;

// Поиск и установка модов/ресурспаков из Modrinth и CurseForge.
public class ModService
{
    private const string ModrinthApi = "https://api.modrinth.com/v2";
    private const string CurseForgeApi = "https://api.curseforge.com/v1";
    // CurseForge требует API-ключ: https://console.curseforge.com/
    private const string CurseForgeKey = "YOUR_CURSEFORGE_API_KEY";
    private const int MinecraftGameId = 432;

    private readonly ApiClient _api;
    private readonly PathService _paths;

    public ModService(ApiClient api, PathService paths)
    {
        _api = api;
        _paths = paths;
    }

    // ===== Modrinth поиск =====
    public async Task<List<ModCard>> SearchModrinthAsync(string query, ContentKind kind, CancellationToken ct = default)
    {
        var type = kind == ContentKind.Mod ? "mod" : "resourcepack";
        var facets = Uri.EscapeDataString($"[[\"project_type:{type}\"]]");
        var url = $"{ModrinthApi}/search?query={Uri.EscapeDataString(query)}&facets={facets}&limit=20";

        var result = await _api.GetJsonAsync<JsonElement>(url, ct);
        var list = new List<ModCard>();
        foreach (var hit in result.GetProperty("hits").EnumerateArray())
        {
            list.Add(new ModCard
            {
                Id = hit.GetProperty("project_id").GetString() ?? "",
                Title = hit.GetProperty("title").GetString() ?? "",
                Description = hit.GetProperty("description").GetString() ?? "",
                IconUrl = hit.TryGetProperty("icon_url", out var ic) ? ic.GetString() ?? "" : "",
                Author = hit.GetProperty("author").GetString() ?? "",
                Downloads = hit.GetProperty("downloads").GetInt32(),
                Source = ModSource.Modrinth
            });
        }
        return list;
    }

    // ===== CurseForge поиск =====
    public async Task<List<ModCard>> SearchCurseForgeAsync(string query, ContentKind kind, CancellationToken ct = default)
    {
        // classId: 6 = моды, 12 = ресурспаки
        int classId = kind == ContentKind.Mod ? 6 : 12;
        var url = $"{CurseForgeApi}/mods/search?gameId={MinecraftGameId}&classId={classId}&searchFilter={Uri.EscapeDataString(query)}&pageSize=20";

        var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
        req.Headers.Add("x-api-key", CurseForgeKey);
        using var resp = await _api.Raw.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));

        var list = new List<ModCard>();
        foreach (var m in json.RootElement.GetProperty("data").EnumerateArray())
        {
            list.Add(new ModCard
            {
                Id = m.GetProperty("id").GetInt32().ToString(),
                Title = m.GetProperty("name").GetString() ?? "",
                Description = m.GetProperty("summary").GetString() ?? "",
                IconUrl = m.TryGetProperty("logo", out var l) && l.ValueKind == JsonValueKind.Object
                    ? l.GetProperty("url").GetString() ?? "" : "",
                Author = m.GetProperty("authors")[0].GetProperty("name").GetString() ?? "",
                Downloads = (int)m.GetProperty("downloadCount").GetDouble(),
                Source = ModSource.CurseForge
            });
        }
        return list;
    }

    // ===== Скачивание выбранного контента =====
    // Сначала узнаём прямую ссылку на последний файл, затем качаем в mods/resourcepacks.
    public async Task<string> DownloadAsync(ModCard card, ContentKind kind,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        string fileUrl, fileName;

        if (card.Source == ModSource.Modrinth)
        {
            var versions = await _api.GetJsonAsync<JsonElement>($"{ModrinthApi}/project/{card.Id}/version", ct);
            var first = versions.EnumerateArray().First();
            var file = first.GetProperty("files").EnumerateArray().First();
            fileUrl = file.GetProperty("url").GetString()!;
            fileName = file.GetProperty("filename").GetString()!;
        }
        else // CurseForge
        {
            var req = new System.Net.Http.HttpRequestMessage(
                System.Net.Http.HttpMethod.Get, $"{CurseForgeApi}/mods/{card.Id}/files");
            req.Headers.Add("x-api-key", CurseForgeKey);
            using var resp = await _api.Raw.SendAsync(req, ct);
            var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var f = json.RootElement.GetProperty("data").EnumerateArray().First();
            fileUrl = f.GetProperty("downloadUrl").GetString()!;
            fileName = f.GetProperty("fileName").GetString()!;
        }

        var dir = kind == ContentKind.Mod ? _paths.ModsDir : _paths.ResourcePacksDir;
        var dest = Path.Combine(dir, fileName);
        await _api.DownloadFileAsync(fileUrl, dest, progress, ct);
        return dest;
    }
}