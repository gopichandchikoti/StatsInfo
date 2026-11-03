using System.Windows.Input;
using System.Windows.Media;
using StatsInfo.Models;

namespace StatsInfo.Services;

public static class SettingsValidation
{
    public static string? Validate(AppSettings s)
    {
        if (s.LatencyEnabled && Uri.CheckHostName(s.LatencyEndpoint.Trim()) == UriHostNameType.Unknown)
            return "Enter a valid probe hostname or IP before enabling probes.";
        if (!TryHotkey(s.ToggleHotkey, out _, out _) || !TryHotkey(s.RecoveryHotkey, out _, out _))
            return "Hotkeys need Ctrl, Alt or Shift plus F1–F24, excluding Windows-reserved F12.";
        if (s.ToggleHotkey.Equals(s.RecoveryHotkey, StringComparison.OrdinalIgnoreCase))
            return "Show/hide and recovery hotkeys must differ.";
        if (s.QuietStartHour is < -1 or > 23 || s.QuietEndHour is < -1 or > 23)
            return "Quiet hours must be -1 (off) or 0–23.";
        if (!Fonts.SystemFontFamilies.Any(f => f.Source.Equals(s.FontFamily, StringComparison.OrdinalIgnoreCase)))
            return "Choose an installed Windows font family (for example Segoe UI).";
        try { _ = ColorConverter.ConvertFromString(s.AccentColor); }
        catch (Exception ex) when (ex is FormatException or NotSupportedException or ArgumentException)
        { return "Enter a valid accent color, such as #3B82F6."; }
        foreach (var rule in s.Alerts.Where(a => a.Enabled))
        {
            if (!MetricRegistry.ById.ContainsKey(rule.MetricId) ||
                !double.IsFinite(rule.Warning) || !double.IsFinite(rule.Critical) || !double.IsFinite(rule.Clear))
                return "Alert thresholds must be finite numbers for a registered metric.";
            bool ordered = rule.Comparison == "<=" ? rule.Critical <= rule.Warning && rule.Clear > rule.Warning
                : rule.Critical >= rule.Warning && rule.Clear < rule.Warning;
            if (!ordered) return "Alert clear threshold must be outside warning; critical must be more severe than warning.";
        }
        return null;
    }

    public static bool TryHotkey(string text, out uint modifiers, out uint key)
    {
        modifiers = 0;
        key = 0;
        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return false;
        foreach (string part in parts[..^1])
        {
            uint modifier = part.ToLowerInvariant() switch { "ctrl" => 2u, "alt" => 1u, "shift" => 4u, _ => 0u };
            if (modifier == 0 || (modifiers & modifier) != 0) return false;
            modifiers |= modifier;
        }
        string last = parts[^1].ToUpperInvariant();
        if (!last.StartsWith('F') || !int.TryParse(last[1..], out int f) || f is < 1 or > 24 or 12) return false;
        key = 0x70u + (uint)f - 1;
        return true;
    }
}
