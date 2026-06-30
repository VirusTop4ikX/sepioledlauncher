using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.Installer.FabricMC;
using GlassLauncher.Core;
using GlassLauncher.Services;

namespace GlassLauncher.Core;

// Обёртка над CmlLib.Core 4.x: список версий, установка (с Forge/Fabric),
// поиск/скачивание Java выполняет сама библиотека, и запуск процесса игры.
public class MinecraftService
{
    private readonly MinecraftLauncher _launcher;
    private readonly MinecraftPath _path;

    // События прогресса для UI
    public event Action<string, int, int>? FileProgress; // имя, сделано, всего
    public event Action<long, long>? ByteProgress;       // байт сделано / всего

    public MinecraftService(SettingsService settings)
    {
        _path = new MinecraftPath(settings.Config.GameDirectory);
        _launcher = new MinecraftLauncher(_path);

        _launcher.FileProgressChanged += (_, a) =>
            FileProgress?.Invoke(a.Name ?? "", a.ProgressedTasks, a.TotalTasks);
        _launcher.ByteProgressChanged += (_, a) =>
            ByteProgress?.Invoke(a.ProgressedBytes, a.TotalBytes);
    }

    // Список всех доступных версий Minecraft (для выпадающего списка).
    public async Task<IEnumerable<string>> GetVersionsAsync()
    {
        var versions = await _launcher.GetAllVersionsAsync();
        return versions.Select(v => v.Name);
    }

    // Установка + запуск выбранного профиля.
    // Для Forge/Fabric сначала устанавливаем загрузчик, затем запускаем его версию.
    public async Task<System.Diagnostics.Process> LaunchAsync(
        LauncherProfile profile, ElyAccount account, CancellationToken ct = default)
    {
        string versionToLaunch = profile.VersionId;

        switch (profile.Loader)
        {
            case LoaderType.Forge:
                var forge = new ForgeInstaller(_launcher);
                versionToLaunch = await forge.Install(profile.VersionId, cancellationToken: ct);
                break;

            case LoaderType.Fabric:
                var fabric = new FabricInstaller(new System.Net.Http.HttpClient());
                versionToLaunch = await fabric.Install(profile.VersionId, _path);
                break;

            case LoaderType.Vanilla:
            default:
                await _launcher.InstallAsync(profile.VersionId, cancellationToken: ct);
                break;
        }

        // Создаём игровую сессию из токена Ely.by.
        // Ely.by совместим с протоколом Mojang yggdrasil через authlib-injector,
        // поэтому достаточно передать username/uuid/accessToken.
        var session = new MSession(account.Username, account.AccessToken, account.Uuid);

        var process = await _launcher.BuildProcessAsync(versionToLaunch, new MLaunchOption
        {
            Session = session,
            MaximumRamMb = profile.RamMb,
            // authlib-injector аргумент можно добавить здесь при необходимости:
            // ExtraJvmArguments = new[] { MArgument.FromCommandLine("-javaagent:authlib-injector.jar=ely.by") }
        }, ct);

        process.Start();
        return process;
    }
}