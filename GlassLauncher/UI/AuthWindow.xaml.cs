using System.Windows;
using GlassLauncher.Core;
using Microsoft.Web.WebView2.Core;

namespace GlassLauncher.UI;

// Окно со встроенным WebView2: открывает страницу Ely.by,
// перехватывает редирект на redirect_uri и достаёт ?code=...
public partial class AuthWindow : Window
{
    private readonly ElyByAuthService _auth;
    public ElyAccount? Account { get; private set; }

    public AuthWindow(ElyByAuthService auth)
    {
        InitializeComponent();
        _auth = auth;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await Web.EnsureCoreWebView2Async();                 // инициализация рантайма
        Web.CoreWebView2.NavigationStarting += OnNavigating; // следим за переходами
        Web.Source = new Uri(_auth.BuildAuthUrl());          // открываем страницу входа
    }

    private async void OnNavigating(object? s, CoreWebView2NavigationStartingEventArgs e)
    {
        // Перехватываем редирект на наш redirect_uri
        if (!e.Uri.StartsWith(ElyByAuthService.RedirectUri)) return;

        e.Cancel = true; // не даём WebView грузить несуществующую страницу
        var query = new Uri(e.Uri).Query.TrimStart('?')
            .Split('&').Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p.Length > 1 ? p[1] : ""));

        if (query.TryGetValue("code", out var code))
        {
            try
            {
                Account = await _auth.ExchangeCodeAsync(code);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка входа: {ex.Message}");
                DialogResult = false;
            }
            Close();
        }
        else if (query.ContainsKey("error"))
        {
            DialogResult = false;
            Close();
        }
    }
}