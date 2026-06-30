using System.Windows;

namespace GlassLauncher.UI;

// Мгновенное переключение тем: подменяем словарь ресурсов темы.
// Анимация смены цветов реализована в стилях через цветовые ресурсы + триггеры.
public static class ThemeManager
{
    public static void Apply(string theme)
    {
        var dictName = theme switch
        {
            "Light" => "UI/Styles/Theme.Light.xaml",
            "Neon"  => "UI/Styles/Theme.Neon.xaml",
            _       => "UI/Styles/Theme.Dark.xaml",
        };

        var merged = Application.Current.Resources.MergedDictionaries;
        // Удаляем предыдущую тему (Theme.*), оставляя Glass.xaml
        for (int i = merged.Count - 1; i >= 0; i--)
        {
            var src = merged[i].Source?.OriginalString ?? "";
            if (src.Contains("Theme.")) merged.RemoveAt(i);
        }
        merged.Add(new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/{dictName}")
        });
    }
}