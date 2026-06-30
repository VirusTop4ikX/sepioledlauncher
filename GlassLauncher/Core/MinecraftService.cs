using System.Net.Http;
using System.Net.Http.Json;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;
using CmlLib.Core.Installer.Forge;
using GlassLauncher.Services;

namespace GlassLauncher.Core;

// Обёртка над CmlLib.Core 4.x.
public class MinecraftService
{
    private readonly MinecraftLauncher _launcher;
    private readonly MinecraftPath _path;
    private readonly SettingsService _settings;
    private static readonly HttpClient _httpClient = new();

    public event Action<string, int, int>? FileProgress;
    public event Action<long, long>? ByteProgress;

    public MinecraftService(SettingsService settings)
    {
        _settings = settings;
        _path = new MinecraftPath(settings.Config.GameDirectory);
        _launcher = new MinecraftLauncher(_path);

        _launcher.FileProgressChanged += (_, a) =>
            FileProgress?.Invoke(a.Name ?? "", a.ProgressedTasks, a.TotalTasks);
        _launcher.ByteProgressChanged += (_, a) =>
            ByteProgress?.Invoke(a.ProgressedBytes, a.TotalBytes);
    }

    public MinecraftPath Path => _path;

    // Версии: релизы (новые сверху); снапшоты по флагу.
    public async Task<List<string>> GetVersionsAsync(bool includeSnapshots = false)
    {
        var versions = await _launcher.GetAllVersionsAsync();

        var releases = versions
            .Where(v => string.Equals(v.Type, "release", StringComparison.OrdinalIgnoreCase))
            .Select(v => v.Name).ToList();

        if (!includeSnapshots) return releases;

        var snapshots = versions
            .Where(v => !string.Equals(v.Type, "release", StringComparison.OrdinalIgnoreCase))
            .Select(v => v.Name).ToList();

        return releases.Concat(snapshots).ToList();
    }

    public async Task<System.Diagnostics.Process> LaunchAsync(
        LauncherProfile profile, ElyAccount account, CancellationToken ct = default)
    {
        string versionToLaunch;

        switch (profile.Loader)
        {
            case LoaderType.Forge:
                versionToLaunch = await new ForgeInstaller(_launcher).Install(profile.VersionId);
                break;

            case LoaderType.Fabric:
                versionToLaunch = await InstallFabricAsync(profile.VersionId, ct);
                break;

            case LoaderType.NeoForge:
                versionToLaunch = await InstallNeoForgeAsync(profile.VersionId, ct);
                break;

            default: // Vanilla
                await _launcher.InstallAsync(profile.VersionId, cancellationToken: ct);
                versionToLaunch = profile.VersionId;
                break;
        }

        MSession session = string.IsNullOrEmpty(account.AccessToken)
            ? MSession.CreateOfflineSession(account.Username)
            : new MSession(account.Username, account.AccessToken, account.Uuid);

        var cfg = _settings.Config;
        var opt = new MLaunchOption
        {
            Session = session,
            MaximumRamMb = profile.RamMb
        };
        if (cfg.ScreenWidth > 0) opt.ScreenWidth = cfg.ScreenWidth;
        if (cfg.ScreenHeight > 0) opt.ScreenHeight = cfg.ScreenHeight;
        if (!string.IsNullOrWhiteSpace(cfg.JavaPath) && System.IO.File.Exists(cfg.JavaPath))
            opt.JavaPath = cfg.JavaPath;
        if (!string.IsNullOrWhiteSpace(cfg.JvmArgs))
            opt.ExtraJvmArguments = cfg.JvmArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(MArgument.FromCommandLine).ToList();

        var process = await _launcher.BuildProcessAsync(versionToLaunch, opt, ct);
        process.Start();
        return process;
    }

    // ===== Fabric через Fabric Meta API =====
    private async Task<string> InstallFabricAsync(string mcVersion, CancellationToken ct)
    {
        var loaders = await _httpClient.GetFromJsonAsync<List<FabricLoaderInfo>>(
            $"https://meta.fabricmc.net/v2/versions/loader/{mcVersion}", ct);
        var loaderVer = loaders?.FirstOrDefault(l => l.loader.stable)?.loader.version
                        ?? loaders?.FirstOrDefault()?.loader.version
                        ?? throw new Exception("Не найден fabric-loader для этой версии");

        var versionName = $"fabric-loader-{loaderVer}-{mcVersion}";
        var json = await _httpClient.GetStringAsync(
            $"https://meta.fabricmc.net/v2/versions/loader/{mcVersion}/{loaderVer}/profile/json", ct);

        WriteVersionJson(versionName, json);
        await _launcher.InstallAsync(versionName, cancellationToken: ct);
        return versionName;
    }

    // ===== NeoForge =====
    // Запуск установленной версии NeoForge. Полноценная установка NeoForge требует
    // их официального инсталлятора; здесь — запуск, если версия уже в versions/.
    private async Task<string> InstallNeoForgeAsync(string mcVersion, CancellationToken ct)
    {
        var installed = (await _launcher.GetAllVersionsAsync())
            .FirstOrDefault(v => v.Name.Contains("neoforge", StringComparison.OrdinalIgnoreCase)
                                 && v.Name.Contains(mcVersion));
        if (installed != null)
        {
            await _launcher.InstallAsync(installed.Name, cancellationToken: ct);
            return installed.Name;
        }
        throw new Exception(
            "NeoForge для этой версии не установлен. Установите его официальным инсталлятором " +
            "NeoForge в ту же папку .minecraft, затем выберите версию NeoForge из списка.");
    }

    private void WriteVersionJson(string versionName, string json)
    {
        var dir = System.IO.Path.Combine(_path.Versions, versionName);
        System.IO.Directory.CreateDirectory(dir);
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, versionName + ".json"), json);
    }

    public class FabricLoaderInfo { public FabricLoaderVersion loader { get; set; } = new(); }
    public class FabricLoaderVersion { public string version { get; set; } = ""; public bool stable { get; set; } }
}