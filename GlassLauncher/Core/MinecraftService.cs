using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;
using CmlLib.Core.Installers;          // FabricInstaller (входит в CmlLib.Core 4.x)
using CmlLib.Core.Installer.Forge;     // ForgeInstaller (пакет CmlLib.Core.Installer.Forge 1.1.1)
using GlassLauncher.Services;           // <-- FabricInstaller (пакет FabricMC)

namespace GlassLauncher.Core;

// Обёртка над CmlLib.Core 4.x: версии, установка (Forge/Fabric), запуск.
// Java библиотека ставит сама при необходимости.
public class MinecraftService
{
    private readonly MinecraftLauncher _launcher;
    private readonly MinecraftPath _path;
    private static readonly System.Net.Http.HttpClient _httpClient = new();

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

    public async Task<IEnumerable<string>> GetVersionsAsync()
    {
        var versions = await _launcher.GetAllVersionsAsync();
        return versions.Select(v => v.Name);
    }

    public async Task<System.Diagnostics.Process> LaunchAsync(
        LauncherProfile profile, ElyAccount account, CancellationToken ct = default)
    {
        string versionToLaunch;

        switch (profile.Loader)
        {
            case LoaderType.Forge:
            {
                // Пакет CmlLib.Core.Installer.Forge 1.1.1
                var forge = new ForgeInstaller(_launcher);
                // Возвращает имя установленной версии, например "1.20.1-forge-47.2.0"
                versionToLaunch = await forge.Install(profile.VersionId);
                break;
            }
            case LoaderType.Fabric:
            {
                // FabricInstaller встроен в CmlLib.Core 4.x (CmlLib.Core.Installers)
                var fabric = new FabricInstaller(new System.Net.Http.HttpClient());
                versionToLaunch = await fabric.Install(profile.VersionId, _path);
                break;
            }
            default: // Vanilla
            {
                await _launcher.InstallAsync(profile.VersionId, cancellationToken: ct);
                versionToLaunch = profile.VersionId;
                break;
            }
        }

        var session = new MSession(account.Username, account.AccessToken, account.Uuid);

        var process = await _launcher.BuildProcessAsync(versionToLaunch, new MLaunchOption
        {
            Session = session,
            MaximumRamMb = profile.RamMb
        }, ct);

        process.Start();
        return process;
    }
}