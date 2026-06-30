using System.Net.Http;
using System.Net.Http.Json;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;
using CmlLib.Core.Installer.Forge;     // ForgeInstaller (пакет CmlLib.Core.Installer.Forge 1.1.1)
using GlassLauncher.Services;

namespace GlassLauncher.Core;

// Обёртка над CmlLib.Core 4.x: версии, установка (Forge/Fabric), запуск.
// Java библиотека ставит сама при необходимости.
public class MinecraftService
{
    private readonly MinecraftLauncher _launcher;
    private readonly MinecraftPath _path;
    private static readonly HttpClient _httpClient = new();

    public event Action<string, int, int>? FileProgress;
    public event Action<long, long>? ByteProgress;

    public MinecraftService(SettingsService settings)
    {
        _path = new MinecraftPath(settings.Config.GameDirectory);
        _launcher = new MinecraftLauncher(_path);

        _launcher.FileProgressChanged += (_, a) =>
            FileProgress?.Invoke(a.Name ?? "", a.ProgressedTasks, a.TotalTasks);
        _launcher.ByteProgressChanged += (_, a) =>
            ByteProgress?.Invoke(a.ProgressedBytes, a.TotalBytes);
    }

    // Версии: релизы (новые сверху). includeSnapshots добавляет снапшоты ниже.
    public async Task<List<string>> GetVersionsAsync(bool includeSnapshots = false)
    {
        var versions = await _launcher.GetAllVersionsAsync();

        var releases = versions
            .Where(v => string.Equals(v.Type, "release", StringComparison.OrdinalIgnoreCase))
            .Select(v => v.Name)
            .ToList();

        if (!includeSnapshots)
            return releases;

        var snapshots = versions
            .Where(v => !string.Equals(v.Type, "release", StringComparison.OrdinalIgnoreCase))
            .Select(v => v.Name)
            .ToList();

        return releases.Concat(snapshots).ToList();
    }

    public async Task<System.Diagnostics.Process> LaunchAsync(
        LauncherProfile profile, ElyAccount account, CancellationToken ct = default)
    {
        string versionToLaunch;

        switch (profile.Loader)
        {
            case LoaderType.Forge:
            {
                var forge = new ForgeInstaller(_launcher);
                versionToLaunch = await forge.Install(profile.VersionId);
                break;
            }
            case LoaderType.Fabric:
            {
                // Узнаём последнюю стабильную версию fabric-loader через Fabric Meta API.
                using var http = new HttpClient();
                var loaders = await http.GetFromJsonAsync<List<FabricLoaderInfo>>(
                    $"https://meta.fabricmc.net/v2/versions/loader/{profile.VersionId}", ct);
                var loaderVer = loaders?.FirstOrDefault(l => l.loader.stable)?.loader.version
                                ?? loaders?.FirstOrDefault()?.loader.version
                                ?? throw new Exception("Не найден fabric-loader для этой версии");

                var versionName = $"fabric-loader-{loaderVer}-{profile.VersionId}";
                var profileJsonUrl =
                    $"https://meta.fabricmc.net/v2/versions/loader/{profile.VersionId}/{loaderVer}/profile/json";

                var versionDir = System.IO.Path.Combine(_path.Versions, versionName);
                System.IO.Directory.CreateDirectory(versionDir);
                var json = await http.GetStringAsync(profileJsonUrl, ct);
                await System.IO.File.WriteAllTextAsync(
                    System.IO.Path.Combine(versionDir, versionName + ".json"), json, ct);

                await _launcher.InstallAsync(versionName, cancellationToken: ct);
                versionToLaunch = versionName;
                break;
            }
            default: // Vanilla
            {
                await _launcher.InstallAsync(profile.VersionId, cancellationToken: ct);
                versionToLaunch = profile.VersionId;
                break;
            }
        }

        // Пустой токен -> offline-сессия (вход по нику); иначе сессия Ely.by.
        MSession session = string.IsNullOrEmpty(account.AccessToken)
            ? MSession.CreateOfflineSession(account.Username)
            : new MSession(account.Username, account.AccessToken, account.Uuid);

        var process = await _launcher.BuildProcessAsync(versionToLaunch, new MLaunchOption
        {
            Session = session,
            MaximumRamMb = profile.RamMb
        }, ct);

        process.Start();
        return process;
    }

    // DTO для Fabric Meta API
    public class FabricLoaderInfo
    {
        public FabricLoaderVersion loader { get; set; } = new();
    }
    public class FabricLoaderVersion
    {
        public string version { get; set; } = "";
        public bool stable { get; set; }
    }
}