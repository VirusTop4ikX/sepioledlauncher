namespace GlassLauncher.Core;

// Профиль запуска: версия, загрузчик, ОЗУ + настройки запуска.
public class LauncherProfile
{
    public string Name { get; set; } = "Default";
    public string VersionId { get; set; } = "1.20.4";
    public LoaderType Loader { get; set; } = LoaderType.Vanilla;
    public int RamMb { get; set; } = 4096;
}

// Добавлен NeoForge
public enum LoaderType { Vanilla, Fabric, Forge, NeoForge }

// Корневой конфиг (config.json)
public class LauncherConfig
{
    public string Theme { get; set; } = "Dark";
    public string GameDirectory { get; set; } = "";
    public string? JavaPath { get; set; }
    public List<LauncherProfile> Profiles { get; set; } = new();
    public string ActiveProfile { get; set; } = "Default";

    // Токены Ely.by
    public string? ElyAccessToken { get; set; }
    public string? ElyRefreshToken { get; set; }
    public string? Username { get; set; }
    public string? Uuid { get; set; }

    // ===== Настройки запуска =====
    public int RamMb { get; set; } = 4096;
    public int ScreenWidth { get; set; } = 0;     // 0 = по умолчанию
    public int ScreenHeight { get; set; } = 0;
    public string JvmArgs { get; set; } = "";
    public bool MinimizeOnLaunch { get; set; } = true;

    // ===== Оффлайн-скин =====
    public string? OfflineSkinPath { get; set; }  // локальный PNG
    public string? OfflineSkinUrl { get; set; }   // ссылка
}

public class ElyAccount
{
    public string Id { get; set; } = "";
    public string Username { get; set; } = "";
    public string Uuid { get; set; } = "";
    public string SkinUrl { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string? RefreshToken { get; set; }
}

// Карточка мода/ресурспака
public class ModCard
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string IconUrl { get; set; } = "";
    public string Author { get; set; } = "";
    public int Downloads { get; set; }
    public ModSource Source { get; set; }
    public string DownloadFileUrl { get; set; } = "";
    public string FileName { get; set; } = "";
}

public enum ModSource { Modrinth, CurseForge }
public enum ContentKind { Mod, ResourcePack }