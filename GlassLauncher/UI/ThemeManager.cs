using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GlassLauncher.UI;

// Переключение темы + современная анимация «старый кадр улетает в кнопку темы».
public static class ThemeManager
{
    public static void Apply(string theme)
    {
        var dictName = theme switch
        {
            "Light" => "UI/Styles/Theme.Light.xaml",
            _ => "UI/Styles/Theme.Dark.xaml",
        };

        var merged = Application.Current.Resources.MergedDictionaries;
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

    // Анимированная смена: снимок текущего вида сжимается и улетает к target.
    public static void ApplyAnimated(string theme, FrameworkElement root, FrameworkElement? target)
    {
        try
        {
            var window = Window.GetWindow(root);
            if (window?.Content is not UIElement content || target == null)
            {
                Apply(theme);
                return;
            }

            // Снимок текущего вида
            var overlay = new System.Windows.Shapes.Rectangle
            {
                Fill = new VisualBrush((Visual)content) { Stretch = Stretch.None },
                IsHitTestVisible = false,
                Width = root.ActualWidth,
                Height = root.ActualHeight,
                RenderTransformOrigin = new Point(0, 0)
            };

            // Кладём overlay поверх через временный Grid
            var hostGrid = new Grid();
            var original = window.Content;
            window.Content = hostGrid;
            hostGrid.Children.Add((UIElement)original);
            hostGrid.Children.Add(overlay);

            // Меняем тему под overlay
            Apply(theme);

            // Куда лететь — позиция кнопки темы
            var to = target.TranslatePoint(new Point(target.ActualWidth / 2, target.ActualHeight / 2), hostGrid);

            var st = new ScaleTransform(1, 1);
            var tt = new TranslateTransform(0, 0);
            var group = new TransformGroup();
            group.Children.Add(st);
            group.Children.Add(tt);
            overlay.RenderTransform = group;
            overlay.RenderTransformOrigin = new Point(0, 0);

            var dur = TimeSpan.FromMilliseconds(420);
            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

            st.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, 0.02, dur) { EasingFunction = ease });
            st.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 0.02, dur) { EasingFunction = ease });
            tt.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, to.X, dur) { EasingFunction = ease });
            tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, to.Y, dur) { EasingFunction = ease });

            var fade = new DoubleAnimation(1, 0, dur) { EasingFunction = ease };
            fade.Completed += (_, _) =>
            {
                hostGrid.Children.Clear();
                window.Content = original; // возвращаем исходное содержимое
            };
            overlay.BeginAnimation(UIElement.OpacityProperty, fade);
        }
        catch
        {
            Apply(theme); // на всякий случай — без анимации
        }
    }
}