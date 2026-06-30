using System.IO;

namespace GlassLauncher.Services;

// Отвечает за пути и создание папок при первом запуске:
// .minecraft, mods, resourcepacks + папка конфига.
public class PathService
{
    public string RootDir { get; }
    public string GameDir { get; }
    public string ModsDir => Path.Combine(GameDir, "mods");
    public string ResourcePacksDir => Path.Combine(GameDir, "resourcepacks");
    public string ConfigPath => Path.Combine(RootDir, "config.json");

    public PathService()
    {
        // Корень — папка рядом с exe (портативный режим)
        RootDir = AppContext.BaseDirectory;
        GameDir = Path.Combine(RootDir, ".minecraft");
        EnsureDirectories();
    }

    // Создаёт все нужные директории, если их ещё нет.
    public void EnsureDirectories()
    {
        Directory.CreateDirectory(GameDir);
        Directory.CreateDirectory(ModsDir);
        Directory.CreateDirectory(ResourcePacksDir);
    }
}