using System.IO;
using System.Text.Json;
using GlassLauncher.Services;

namespace GlassLauncher.Core;

// Поиск и установка модов/ресурспаков из Modrinth (и CurseForge при наличии ключа).
public class ModService
{
    private const string ModrinthApi = "https://api.modrinth.com/v2";

    private readonly ApiClient _api;
    private readonly PathService _paths;

    public ModService(ApiClient api, PathService paths)
    {
        _api = api;
        _paths = paths;
    }

    // Поиск (Modrinth). kind = Mod | ResourcePack
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

    // Скачивание контента в нужную папку (mods или resourcepacks).
    public async Task<string> DownloadAsync(ModCard card, ContentKind kind,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var versions = await _api.GetJsonAsync<JsonElement>($"{ModrinthApi}/project/{card.Id}/version", ct);
        var first = versions.EnumerateArray().First();
        var file = first.GetProperty("files").EnumerateArray().First();
        var fileUrl = file.GetProperty("url").GetString()!;
        var fileName = file.GetProperty("filename").GetString()!;

        var dir = kind == ContentKind.Mod ? _paths.ModsDir : _paths.ResourcePacksDir;
        var dest = Path.Combine(dir, fileName);
        await _api.DownloadFileAsync(fileUrl, dest, progress, ct);
        return dest;
    }

    // Установка CustomSkinLoader (для отображения оффлайн-скина в игре).
    // Качает мод по slug 'customskinloader' с Modrinth в папку mods.
    public async Task<string> InstallCustomSkinLoaderAsync(IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var card = new ModCard { Id = "customskinloader", Source = ModSource.Modrinth };
        return await DownloadAsync(card, ContentKind.Mod, progress, ct);
    }
}