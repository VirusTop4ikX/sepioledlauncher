using System;
using System.Windows;
using System.Windows.Threading;
using GlassLauncher.Core;
using GlassLauncher.Services;
using GlassLauncher.UI;
using GlassLauncher.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GlassLauncher;

public partial class App : Application
{
    private IServiceProvider _services = null!;

    public static void UiDispatch(Action action)
    {
        var app = Current;
        if (app == null) { action(); return; }
        if (app.Dispatcher.CheckAccess()) action();
        else app.Dispatcher.BeginInvoke(action, DispatcherPriority.Background);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var sc = new ServiceCollection();

        // Сервисы
        sc.AddSingleton<SettingsService>();
        sc.AddSingleton<PathService>();
        sc.AddSingleton<ApiClient>();
        sc.AddSingleton<MinecraftService>();
        sc.AddSingleton<ElyByAuthService>();
        sc.AddSingleton<ModService>();

        // VM + окно
        sc.AddSingleton<MainViewModel>();
        sc.AddSingleton<MainWindow>();

        _services = sc.BuildServiceProvider();

        // Применяем сохранённую тему ДО показа окна (без анимации)
        var settings = _services.GetRequiredService<SettingsService>();
        ThemeManager.Apply(settings.Config.Theme ?? "Dark");

        var window = _services.GetRequiredService<MainWindow>();

        // Синхронизируем выбранную тему в комбобоксе
        window.Loaded += (_, _) =>
        {
            if (window.FindName("ThemeCombo") is System.Windows.Controls.ComboBox cb)
                cb.SelectedItem = settings.Config.Theme ?? "Dark";
        };

        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _services?.GetService<SettingsService>()?.Save(); } catch { }
        base.OnExit(e);
    }
}