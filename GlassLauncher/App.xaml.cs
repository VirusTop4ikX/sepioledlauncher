using System.Net.Http;
using System.Windows;
using GlassLauncher.Core;
using GlassLauncher.Services;
using GlassLauncher.UI;
using GlassLauncher.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GlassLauncher;

// Точка входа: настраивает DI-контейнер и показывает главное окно.
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    // Удобный хелпер для маршалинга в UI-поток.
    public static void UiDispatch(Action a) => Current.Dispatcher.Invoke(a);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var sc = new ServiceCollection();

        // ===== Регистрация сервисов (DI) =====
        sc.AddSingleton<PathService>();
        sc.AddSingleton<SettingsService>();
        sc.AddHttpClient();                       // IHttpClientFactory
        sc.AddTransient(sp =>
            new ApiClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient()));
        sc.AddSingleton<ElyByAuthService>();
        sc.AddSingleton<MinecraftService>();
        sc.AddSingleton<ModService>();
        sc.AddSingleton<MainViewModel>();
        sc.AddSingleton<MainWindow>();

        Services = sc.BuildServiceProvider();

        // Создаём папки и применяем сохранённую тему
        var settings = Services.GetRequiredService<SettingsService>();
        ThemeManager.Apply(settings.Config.Theme);

        // Показываем главное окно
        var window = Services.GetRequiredService<MainWindow>();
        window.Show();
    }
}