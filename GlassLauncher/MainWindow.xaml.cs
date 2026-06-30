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
        Loaded += async (_, _) => await _vm.InitCommand.ExecuteAsync(null);
    }

    // Смена темы из ComboBox
    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is string theme)
            _vm.ChangeThemeCommand.Execute(theme);
    }
}