namespace GlassLauncher.Core;

// ====== Профиль запуска ======
// Хранит выбранную версию, тип загрузчика и объём ОЗУ.
public class LauncherProfile
{
    public string Name { get; set; } = "Default";
    public string VersionId { get; set; } = "1.20.4";   // id версии Minecraft
    public LoaderType Loader { get; set; } = LoaderType.Vanilla;
    public int RamMb { get; set; } = 4096;              // выделяемая память
}

public enum LoaderType { Vanilla, Forge, Fabric }

// ====== Корневой конфиг лаунчера (сериализуется в config.json) ======
public class LauncherConfig
{
    public string Theme { get; set; } = "Dark";              // Dark | Light | Neon
    public string GameDirectory { get; set; } = "";          // путь к .minecraft
    public string? JavaPath { get; set; }                    // явный путь к java (опционально)
    public List<LauncherProfile> Profiles { get; set; } = new();
    public string ActiveProfile { get; set; } = "Default";

    // Сохранённые токены Ely.by (для авто-логина)
    public string? ElyAccessToken { get; set; }
    public string? ElyRefreshToken { get; set; }
    public string? Username { get; set; }
    public string? Uuid { get; set; }
}

// ====== Аккаунт Ely.by ======
public class ElyAccount
{
    public string Id { get; set; } = "";
    public string Username { get; set; } = "";
    public string Uuid { get; set; } = "";
    public string SkinUrl { get; set; } = "";   // URL аватара/скина
    public string AccessToken { get; set; } = "";
    public string? RefreshToken { get; set; }
}

// ====== Карточка мода/ресурспака (унифицированная для Modrinth и CurseForge) ======
public class ModCard
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string IconUrl { get; set; } = "";
    public string Author { get; set; } = "";
    public int Downloads { get; set; }
    public ModSource Source { get; set; }
    public string DownloadFileUrl { get; set; } = ""; // прямая ссылка на файл (заполняется при выборе)
    public string FileName { get; set; } = "";
}

public enum ModSource { Modrinth, CurseForge }
public enum ContentKind { Mod, ResourcePack }