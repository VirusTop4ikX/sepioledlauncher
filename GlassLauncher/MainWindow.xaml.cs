using System;
using System.Windows;
using System.Windows.Controls;
using GlassLauncher.ViewModels;

namespace GlassLauncher;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = _vm = vm;

        Loaded += async (_, _) =>
        {
            try { await _vm.InitCommand.ExecuteAsync(null); }
            catch (Exception ex) { MessageBox.Show(ex.ToString(), "Ошибка инициализации"); }
        };
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is string theme)
            _vm.ChangeThemeCommand.Execute(theme);
    }
}