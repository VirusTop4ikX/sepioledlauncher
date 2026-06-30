using System;
using System.Windows;
using System.Windows.Controls;
using GlassLauncher.UI;
using GlassLauncher.ViewModels;

namespace GlassLauncher;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private bool _themeReady; // не анимируем первичную установку

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = _vm = vm;

        Loaded += async (_, _) =>
        {
            try
            {
                // Выставляем текущую тему в комбобоксе без анимации
                ThemeCombo.SelectedItem = string.IsNullOrEmpty(_vm?.SelectedVersion) ? "Dark" : ThemeCombo.SelectedItem;
                if (ThemeCombo.SelectedItem == null) ThemeCombo.SelectedItem = "Dark";
                _themeReady = true;

                await _vm.InitCommand.ExecuteAsync(null);
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString(), "Ошибка инициализации"); }
        };
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox cb || cb.SelectedItem is not string theme) return;

        if (!_themeReady)
        {
            // Первичная установка — без анимации
            ThemeManager.Apply(theme);
            _vm.ChangeThemeCommand.Execute(theme);
            return;
        }

        // Современная анимация: текущий кадр сжимается и улетает в кнопку выбора темы
        ThemeManager.ApplyAnimated(theme, RootGrid, ThemeCombo);
        _vm.ChangeThemeCommand.Execute(theme);
    }
}