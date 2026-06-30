using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;
using CmlLib.Core.Installers;          // <-- ForgeInstaller, ServerInstaller (4.x)
using CmlLib.Core.Installer.Forge;     // <-- неймспейс пакета Forge installer 3.x
using CmlLib.Core.FabricMC;            // <-- FabricInstaller (пакет FabricMC)
using GlassLauncher.Services;

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
                // В 4.x установщик Forge берёт launcher + HttpClient.
                var forge = new ForgeInstaller(_launcher);
                // Вернёт имя установленной версии (например "1.20.1-forge-47.2.0")
                versionToLaunch = await forge.Install(profile.VersionId, new ForgeInstallOptions
                {
                    SkipIfAlreadyInstalled = true
                });
                break;
            }
            case LoaderType.Fabric:
            {
                // FabricInstaller создаётся с HttpClient, ставит в указанный path.
                var fabric = new FabricInstaller(_httpClient);
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