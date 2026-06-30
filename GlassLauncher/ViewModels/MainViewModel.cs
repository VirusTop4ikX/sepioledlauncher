using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassLauncher.Core;
using GlassLauncher.Services;
using GlassLauncher.UI;

namespace GlassLauncher.ViewModels;

// Главная модель представления: связывает UI с сервисами.
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

    public ObservableCollection<string> Versions { get; } = new();
    public ObservableCollection<ModCard> Mods { get; } = new();
    public ObservableCollection<string> Themes { get; } = new() { "Dark", "Light" };

    private ElyAccount? _account;

    public MainViewModel(SettingsService settings, MinecraftService mc,
        ElyByAuthService auth, ModService mods, IServiceProvider sp)
    {
        _settings = settings; _mc = mc; _auth = auth; _mods = mods; _sp = sp;

        _mc.FileProgress += (name, done, total) =>
            App.UiDispatch(() => { Status = $"Загрузка: {name}"; });
        _mc.ByteProgress += (done, total) =>
            App.UiDispatch(() => { if (total > 0) Progress = (double)done / total * 100; });

        try { SelectedVersion = _settings.GetActiveProfile().VersionId; }
        catch { SelectedVersion = null; }
    }

    // Старт: загрузка версий + авто-логин
    [RelayCommand]
    private async Task InitAsync()
    {
        await ReloadVersionsAsync();

        if (!string.IsNullOrEmpty(_settings.Config.ElyRefreshToken))
        {
            try
            {
                _account = await _auth.RefreshAsync(_settings.Config.ElyRefreshToken);
                ApplyAccount(_account);
            }
            catch { Status = "Сессия Ely.by истекла — войдите заново"; }
        }
    }

    // Перезагрузка списка версий
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

    // Авто-перезагрузка при смене флажка снапшотов
    partial void OnIncludeSnapshotsChanged(bool value) => _ = ReloadVersionsAsync();

    // Вход через Ely.by (WebView2)
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
        catch (Exception ex)
        {
            Status = $"Ошибка входа: {ex.Message}";
            MessageBox.Show(ex.ToString(), "Ошибка входа Ely.by");
        }
    }

    // Вход по нику (offline)
    [RelayCommand]
    private void LoginOffline()
    {
        var name = (OfflineUsername ?? "").Trim();
        if (name.Length < 3) { Status = "Ник должен быть не короче 3 символов"; return; }

        _account = new ElyAccount
        {
            Username = name,
            AccessToken = "",
            Uuid = "",
            SkinUrl = $"https://mc-heads.net/avatar/{name}/96"
        };
        ApplyAccount(_account);

        _settings.Config.Username = name;
        _settings.Config.ElyAccessToken = null;
        _settings.Config.ElyRefreshToken = null;
        _settings.Save();
        Status = $"Оффлайн-вход: {name}";
    }

    private void ApplyAccount(ElyAccount acc)
    {
        Username = acc.Username;
        SkinUrl = acc.SkinUrl;
        Status = $"Вы вошли как {acc.Username}";
    }

    // Запуск игры
    [RelayCommand]
    private async Task PlayAsync()
    {
        if (_account == null) { Status = "Сначала выполните вход"; return; }
        if (string.IsNullOrEmpty(SelectedVersion)) { Status = "Выберите версию"; return; }

        IsBusy = true; Progress = 0;
        try
        {
            var profile = _settings.GetActiveProfile();
            profile.VersionId = SelectedVersion!;
            _settings.Save();

            Status = "Подготовка...";
            await _mc.LaunchAsync(profile, _account);
            Status = "Игра запущена!";
        }
        catch (Exception ex) { Status = $"Ошибка запуска: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    // Поиск модов (Modrinth)
    [RelayCommand]
    private async Task SearchModsAsync(string query)
    {
        IsBusy = true;
        try
        {
            Mods.Clear();
            var results = await _mods.SearchModrinthAsync(query ?? "", ContentKind.Mod);
            foreach (var m in results) Mods.Add(m);
            Status = $"Найдено модов: {Mods.Count}";
        }
        catch (Exception ex) { Status = $"Ошибка поиска: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    // Скачать выбранный мод
    [RelayCommand]
    private async Task DownloadModAsync(ModCard? card)
    {
        if (card == null) return;
        IsBusy = true; Progress = 0;
        try
        {
            var prog = new Progress<double>(p => App.UiDispatch(() => Progress = p));
            var path = await _mods.DownloadAsync(card, ContentKind.Mod, prog);
            Status = $"Установлено: {System.IO.Path.GetFileName(path)}";
        }
        catch (Exception ex) { Status = $"Ошибка загрузки: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    // Смена темы
    [RelayCommand]
    private void ChangeTheme(string theme)
    {
        try
        {
            ThemeManager.Apply(theme);
            _settings.Config.Theme = theme;
            _settings.Save();
        }
        catch (Exception ex) { Status = $"Ошибка темы: {ex.Message}"; }
    }
}