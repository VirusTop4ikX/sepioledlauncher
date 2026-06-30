using System.IO;
using System.Text.Json;
using GlassLauncher.Core;

namespace GlassLauncher.Services;

// Загрузка/сохранение config.json и управление профилями.
public class SettingsService
{
    private readonly PathService _paths;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public LauncherConfig Config { get; private set; } = new();

    public SettingsService(PathService paths)
    {
        _paths = paths;
        Load();
    }

    public void Load()
    {
        if (File.Exists(_paths.ConfigPath))
        {
            try
            {
                var json = File.ReadAllText(_paths.ConfigPath);
                Config = JsonSerializer.Deserialize<LauncherConfig>(json) ?? new LauncherConfig();
            }
            catch { Config = new LauncherConfig(); }
        }

        // При первом запуске создаём профиль по умолчанию
        if (Config.Profiles.Count == 0)
            Config.Profiles.Add(new LauncherProfile());

        if (string.IsNullOrEmpty(Config.GameDirectory))
            Config.GameDirectory = _paths.GameDir;

        Save();
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(Config, JsonOpts);
        File.WriteAllText(_paths.ConfigPath, json);
    }

    public LauncherProfile GetActiveProfile() =>
        Config.Profiles.FirstOrDefault(p => p.Name == Config.ActiveProfile)
        ?? Config.Profiles.First();

    public void AddProfile(LauncherProfile profile)
    {
        Config.Profiles.Add(profile);
        Save();
    }
}