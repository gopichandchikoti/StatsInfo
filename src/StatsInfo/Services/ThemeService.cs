using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using StatsInfo.Models;

namespace StatsInfo.Services;

public static class ThemeService
{
    public static void Apply(Window window, AppSettings settings)
    {
        bool contrast = SystemParameters.HighContrast || settings.Theme == "High Contrast";
        bool light = settings.Theme == "Light";
        if (settings.Theme == "System")
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            light = (int?)key?.GetValue("AppsUseLightTheme") == 1;
        }
        Brush background = contrast ? SystemColors.WindowBrush : new SolidColorBrush(light
            ? Color.FromRgb(245, 246, 250) : Color.FromRgb(24, 24, 37));
        Brush foreground = contrast ? SystemColors.WindowTextBrush : new SolidColorBrush(light
            ? Color.FromRgb(31, 35, 48) : Color.FromRgb(205, 214, 244));
        Brush muted = contrast ? SystemColors.WindowTextBrush : new SolidColorBrush(light
            ? Color.FromRgb(75, 81, 100) : Color.FromRgb(166, 173, 200));
        Brush accent;
        try { accent = contrast ? SystemColors.HighlightBrush : new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.AccentColor)); }
        catch (Exception ex) when (ex is FormatException or NotSupportedException or ArgumentException)
        { accent = Brushes.CornflowerBlue; }
        foreach (var key in new[] { "PageBackground", "OverlayBackground" }) window.Resources[key] = background;
        foreach (var key in new[] { "PageForeground", "OverlayForeground" }) window.Resources[key] = foreground;
        window.Resources["MutedForeground"] = muted;
        window.Resources["OverlayAccent"] = accent;
        if (contrast) window.Opacity = 1;
    }
}
