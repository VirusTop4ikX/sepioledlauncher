using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassLauncher.Core;
using GlassLauncher.Services;
using GlassLauncher.UI;
using Microsoft.Win32;

namespace GlassLauncher.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly SettingsService _settings;
    private readonly MinecraftService _mc;
    private readonly ElyByAuthService _auth;
    private readonly ModService _mods;
    private readonly IServiceProvider _sp;

    [ObservableProperty] private string? _username = "Гость";
    [ObservableProperty] private string? _skinUrl;
    [ObservableProperty] private string? _selectedVersion;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _status = "Готов к запуску";
    [ObservableProperty] private bool _includeSnapshots;
    [ObservableProperty] private string _offlineUsername = "Player";
    [ObservableProperty] private string _selectedLoader = "Vanilla";
    [ObservableProperty] private string _selectedContentKind = "Моды";

    // Настройки запуска (привязаны к UI вкладки "Настройки")
    [ObservableProperty] private int _ramMb;
    [ObservableProperty] private int _screenWidth;
    [ObservableProperty] private int _screenHeight;
    [ObservableProperty] private string _jvmArgs = "";
    [ObservableProperty] private string? _javaPath;
    [ObservableProperty] private string _gameDirectory = "";
    [ObservableProperty] private bool _minimizeOnLaunch;

    public ObservableCollection<string> Versions { get; } = new();
    public ObservableCollection<ModCard> Mods { get; } = new();
    public ObservableCollection<string> Themes { get; } = new() { "Dark", "Light" };
    public ObservableCollection<string> Loaders { get; } = new() { "Vanilla", "Fabric", "Forge", "NeoForge" };
    public ObservableCollection<string> ContentKinds { get; } = new() { "Моды", "Ресурспаки" };

    private ElyAccount? _account;

    public MainViewModel(SettingsService settings, MinecraftService mc,
        ElyByAuthService auth, ModService mods, IServiceProvider sp)
    {
        _settings = settings; _mc = mc; _auth = auth; _mods = mods; _sp = sp;

        _mc.FileProgress += (name, done, total) =>
            App.UiDispatch(() => { Status = $"Загрузка: {name}"; });
        _mc.ByteProgress += (done, total) =>
            App.UiDispatch(() => { if (total > 0) Progress = (double)done / total * 100; });

        // Подтягиваем настройки из конфига
        var c = _settings.Config;
        RamMb = c.RamMb;
        ScreenWidth = c.ScreenWidth;
        ScreenHeight = c.ScreenHeight;
        JvmArgs = c.JvmArgs;
        JavaPath = c.JavaPath;
        GameDirectory = c.GameDirectory;
        MinimizeOnLaunch = c.MinimizeOnLaunch;

        try { SelectedVersion = _settings.GetActiveProfile().VersionId; } catch { SelectedVersion = null; }
    }

    [RelayCommand]
    private async Task InitAsync()
    {
        await ReloadVersionsAsync();

        // Восстановить оффлайн-скин/ник, если был
        if (!string.IsNullOrEmpty(_settings.Config.ElyRefreshToken))
        {
            try { _account = await _auth.RefreshAsync(_settings.Config.ElyRefreshToken); ApplyAccount(_account); }
            catch { Status = "Сессия Ely.by истекла — войдите заново"; }
        }
    }

    [RelayCommand]
    private async Task ReloadVersionsAsync()
    {
        try
        {
            Versions.Clear();
            var versions = await _mc.GetVersionsAsync(IncludeSnapshots);
            foreach (var v in versions) Versions.Add(v);
            if (string.IsNullOrEmpty(SelectedVersion) || !Versions.Contains(SelectedVersion))
                SelectedVersion = Versions.FirstOrDefault();
            Status = $"Версий: {Versions.Count}";
        }
        catch (Exception ex) { Status = $"Не удалось загрузить версии: {ex.Message}"; }
    }

    partial void OnIncludeSnapshotsChanged(bool value) => _ = ReloadVersionsAsync();

    // ===== Вход Ely.by =====
    [RelayCommand]
    private void Login()
    {
        try
        {
            var win = new AuthWindow(_auth) { Owner = Application.Current.MainWindow };
            if (win.ShowDialog() == true && win.Account != null)
            {
                _account = win.Account;
                ApplyAccount(_account);
                _settings.Config.ElyAccessToken = _account.AccessToken;
                _settings.Config.ElyRefreshToken = _account.RefreshToken;
                _settings.Config.Username = _account.Username;
                _settings.Config.Uuid = _account.Uuid;
                _settings.Save();
            }
        }
        catch (Exception ex) { Status = $"Ошибка входа: {ex.Message}"; }
    }

    // ===== Оффлайн-вход =====
    [RelayCommand]
    private void LoginOffline()
    {
        var name = (OfflineUsername ?? "").Trim();
        if (name.Length < 3) { Status = "Ник должен быть не короче 3 символов"; return; }

        // Приоритет скина: локальный файл -> ссылка -> по нику с mc-heads
        string skin =
            !string.IsNullOrWhiteSpace(_settings.Config.OfflineSkinPath) && File.Exists(_settings.Config.OfflineSkinPath)
                ? _settings.Config.OfflineSkinPath!
            : !string.IsNullOrWhiteSpace(_settings.Config.OfflineSkinUrl)
                ? _settings.Config.OfflineSkinUrl!
                : $"https://mc-heads.net/avatar/{name}/96";

        _account = new ElyAccount { Username = name, AccessToken = "", Uuid = "", SkinUrl = skin };
        ApplyAccount(_account);

        _settings.Config.Username = name;
        _settings.Config.ElyAccessToken = null;
        _settings.Config.ElyRefreshToken = null;
        _settings.Save();
        Status = $"Оффлайн-вход: {name}";
    }

    // Выбрать локальный PNG-скин
    [RelayCommand]
    private void PickSkinFile()
    {
        var dlg = new OpenFileDialog { Filter = "PNG изображения (*.png)|*.png", Title = "Выберите скин" };
        if (dlg.ShowDialog() == true)
        {
            _settings.Config.OfflineSkinPath = dlg.FileName;
            _settings.Config.OfflineSkinUrl = null;
            _settings.Save();
            SkinUrl = dlg.FileName;
            Status = "Скин из файла установлен";
        }
    }

    // Установить скин по ссылке
    [RelayCommand]
    private void SetSkinUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) { Status = "Введите ссылку на скин"; return; }
        _settings.Config.OfflineSkinUrl = url.Trim();
        _settings.Config.OfflineSkinPath = null;
        _settings.Save();
        SkinUrl = url.Trim();
        Status = "Скин по ссылке установлен";
    }

    // Установить CustomSkinLoader (чтобы скин был виден в игре в offline)
    [RelayCommand]
    private async Task InstallSkinModAsync()
    {
        IsBusy = true; Progress = 0;
        try
        {
            var prog = new Progress<double>(p => App.UiDispatch(() => Progress = p));
            var path = await _mods.InstallCustomSkinLoaderAsync(prog);
            Status = $"Установлен мод скинов: {Path.GetFileName(path)}";
        }
        catch (Exception ex) { Status = $"Не удалось установить мод скинов: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private void ApplyAccount(ElyAccount acc)
    {
        Username = acc.Username;
        SkinUrl = acc.SkinUrl;
        Status = $"Вы вошли как {acc.Username}";
    }

    // ===== Запуск =====
    [RelayCommand]
    private async Task PlayAsync()
    {
        if (_account == null) { Status = "Сначала выполните вход"; return; }
        if (string.IsNullOrEmpty(SelectedVersion)) { Status = "Выберите версию"; return; }

        SaveSettings(); // фиксируем настройки запуска
        IsBusy = true; Progress = 0;
        try
        {
            var profile = _settings.GetActiveProfile();
            profile.VersionId = SelectedVersion!;
            profile.RamMb = RamMb > 0 ? RamMb : 4096;
            profile.Loader = SelectedLoader switch
            {
                "Fabric" => LoaderType.Fabric,
                "Forge" => LoaderType.Forge,
                "NeoForge" => LoaderType.NeoForge,
                _ => LoaderType.Vanilla
            };
            _settings.Save();

            Status = "Подготовка...";
            var proc = await _mc.LaunchAsync(profile, _account);
            Status = "Игра запущена!";

            if (MinimizeOnLaunch && Application.Current.MainWindow != null)
                Application.Current.MainWindow.WindowState = WindowState.Minimized;
        }
        catch (Exception ex) { Status = $"Ошибка запуска: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    // ===== Моды / Ресурспаки =====
    [RelayCommand]
    private async Task SearchModsAsync(string query)
    {
        IsBusy = true;
        try
        {
            Mods.Clear();
            var kind = SelectedContentKind == "Ресурспаки" ? ContentKind.ResourcePack : ContentKind.Mod;
            var results = await _mods.SearchModrinthAsync(query ?? "", kind);
            foreach (var m in results) Mods.Add(m);
            Status = $"Найдено: {Mods.Count}";
        }
        catch (Exception ex) { Status = $"Ошибка поиска: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DownloadModAsync(ModCard? card)
    {
        if (card == null) return;
        IsBusy = true; Progress = 0;
        try
        {
            var kind = SelectedContentKind == "Ресурспаки" ? ContentKind.ResourcePack : ContentKind.Mod;
            var prog = new Progress<double>(p => App.UiDispatch(() => Progress = p));
            var path = await _mods.DownloadAsync(card, kind, prog);
            Status = $"Установлено: {Path.GetFileName(path)}";
        }
        catch (Exception ex) { Status = $"Ошибка загрузки: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    // ===== Настройки =====
    [RelayCommand]
    private void SaveSettings()
    {
        var c = _settings.Config;
        c.RamMb = RamMb > 0 ? RamMb : 4096;
        c.ScreenWidth = ScreenWidth;
        c.ScreenHeight = ScreenHeight;
        c.JvmArgs = JvmArgs ?? "";
        c.JavaPath = JavaPath;
        c.GameDirectory = string.IsNullOrWhiteSpace(GameDirectory) ? c.GameDirectory : GameDirectory;
        c.MinimizeOnLaunch = MinimizeOnLaunch;
        _settings.Save();
        Status = "Настройки сохранены";
    }

    [RelayCommand]
    private void PickJavaPath()
    {
        var dlg = new OpenFileDialog { Filter = "Java (java.exe;javaw.exe)|java.exe;javaw.exe", Title = "Выберите java" };
        if (dlg.ShowDialog() == true) { JavaPath = dlg.FileName; SaveSettings(); }
    }

    [RelayCommand]
    private void PickGameDir()
    {
        var dlg = new OpenFolderDialog { Title = "Выберите папку .minecraft" };
        if (dlg.ShowDialog() == true) { GameDirectory = dlg.FolderName; SaveSettings(); }
    }

    // ===== Тема (с анимацией задаётся из code-behind) =====
    [RelayCommand]
    private void ChangeTheme(string theme)
    {
        _settings.Config.Theme = theme;
        _settings.Save();
    }
}