using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
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

    // Путь к лог-файлу рядом с exe — туда пишем любые ошибки.
    private static readonly string LogPath =
        Path.Combine(AppContext.BaseDirectory, "error.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ===== Глобальный перехват ошибок (показ + запись в файл) =====
        DispatcherUnhandledException += (s, ex) =>
        {
            LogError("UI-поток", ex.Exception);
            MessageBox.Show(ex.Exception.ToString(), "Ошибка UI-потока");
            ex.Handled = true; // не даём приложению упасть
        };
        AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
        {
            var error = ex.ExceptionObject as Exception;
            LogError("Домен", error);
            MessageBox.Show(error?.ToString() ?? "Неизвестная ошибка", "Необработанная ошибка");
        };
        TaskScheduler.UnobservedTaskException += (s, ex) =>
        {
            LogError("Фоновая задача", ex.Exception);
            ex.SetObserved();
        };

        try
        {
            var sc = new ServiceCollection();

            // ===== Регистрация сервисов (DI) =====
            sc.AddSingleton<PathService>();
            sc.AddSingleton<SettingsService>();
            sc.AddHttpClient(); // IHttpClientFactory

            // ApiClient теперь singleton — однородный граф зависимостей.
            sc.AddSingleton<ApiClient>(sp =>
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
        catch (Exception ex)
        {
            // Ошибка на этапе старта/DI — показываем и логируем.
            LogError("Старт приложения", ex);
            MessageBox.Show(ex.ToString(), "Ошибка запуска");
            Shutdown();
        }
    }

    // Запись ошибки в error.log
    private static void LogError(string source, Exception? ex)
    {
        try
        {
            File.AppendAllText(LogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}]{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch { /* логирование не должно ронять приложение */ }
    }
}