using System.IO;
using System.Windows;
using System.Windows.Controls;
using StatsInfo.Helpers;
using StatsInfo.Models;
using StatsInfo.Services;

namespace StatsInfo.Views;

public partial class SettingsWindow
{
    private void BuildPanels()
    {
        AddPanel("Overlay & Appearance", BuildAppearance);
        AddPanel("Sampling & Performance", BuildSampling);
        AddPanel("Alerts", BuildAlerts);
        AddPanel("Network & FPS", BuildNetwork);
        AddPanel("History & Export", BuildHistory);
        AddPanel("Hardware & Diagnostics", BuildDiagnostics);
        AddPanel("General", p =>
        {
            p.Check("Launch at Windows sign-in (current user, opt-in)", "StartWithWindows");
            p.Hint("StatsInfo 2.0 · Windows 10/11 · unsigned local preview. No telemetry, automatic updates, " +
                "bandwidth tests or cloud services. The tray remains available when the overlay is hidden.");
            p.Action("Recover overlay position and input", (_, _) => RecoveryRequested?.Invoke());
            p.Hint($"Settings and opt-in history: {_viewModel.Service.SettingsPath}\n" +
                "No serial numbers or process command lines are collected. Sound, probes, history persistence " +
                "and privileged sensors are opt-in. Close the overlay to exit; use the tray to hide it.");
        });
        ThemeService.Apply(this, _viewModel.Service.Settings);
    }

    private static void BuildAppearance(SettingsPanelBuilder p)
    {
        p.Choice("Layout", "Layout", new[] { "Horizontal", "Vertical", "Compact Gaming", "Expanded" });
        p.Choice("Theme", "Theme", new[] { "Dark", "Light", "System", "High Contrast" });
        p.Slider("Opacity (0.2–1; 1 is opaque)", "Opacity", 0.2, 1);
        p.Slider("Scale (0.75–2)", "Scale", 0.75, 2);
        p.Slider("Font size", "FontSize", 10, 24);
        p.Text("Font family (installed Windows font)", "FontFamily");
        p.Slider("Space between metrics", "MetricSpacing", 0, 24);
        p.Text("Accent color (#RRGGBB)", "AccentColor");
        p.Choice("Density", "Density", new[] { "Comfortable", "Compact" });
        var monitors = DisplayInterop.Monitors().Select((m, i) => new MonitorChoice(i, m.Label)).ToArray();
        p.Choice("Target monitor", "TargetMonitorIndex", monitors, "Label", "Index");
        p.Choice("Anchor", "Position", new[] { "TopLeft", "TopCenter", "TopRight", "Custom" });
        p.Check("Lock overlay position", "LockPosition");
        p.Check("Always on Top", "AlwaysOnTop");
        p.Check("Click-through (tray and recovery hotkey remain available)", "ClickThrough");
        p.Check("Hide overlay while another app is fullscreen on this monitor", "HideInFullscreen");
        p.Text("Show/hide hotkey", "ToggleHotkey");
        p.Text("Recovery hotkey (disables click-through and repositions)", "RecoveryHotkey");
        p.Hint("Use Ctrl/Alt/Shift plus F1–F24, excluding reserved F12. Recovery defaults to Ctrl+Shift+F10. " +
            "Conflicts are reported on Apply. " +
            "Native opaque/translucent backgrounds are supported; blur is not enabled. No decorative motion.");
    }

    private void BuildSampling(SettingsPanelBuilder p)
    {
        p.Text("Default update interval (ms; 500–10000)", "UpdateIntervalMs");
        p.Check("Pause all monitoring (stops active FPS capture)", "Paused");
        p.Check("Low-impact mode (at least 3 seconds per category)", "LowImpactMode");
        p.Check("Auto-pause on battery", "AutoPauseOnBattery");
        p.Hint("Only metrics required by visibility, alerts, diagnostics or an open history view are sampled. " +
            "Changing visibility never disables an alert. Category intervals are milliseconds.");
        foreach (string category in new[] { "CPU", "Memory", "GPU", "Storage", "Network", "System/Cooling",
            "Process", "Display/Gaming", "Sensors", "FPS" })
        {
            p.Label(category);
            var input = new TextBox
            {
                Text = _draft.CategoryIntervals
                .GetValueOrDefault(category, _draft.UpdateIntervalMs).ToString()
            };
            SettingsPanelBuilder.Name(input, $"{category} interval");
            p.Panel.Children.Add(input);
            _categoryEditors[category] = input;
        }
        _overhead = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 0) };
        p.Panel.Children.Add(_overhead);
    }

    private void BuildAlerts(SettingsPanelBuilder p)
    {
        p.Hint("Thresholds use canonical registry units, independently of display units. " +
            "Examples are editable, not hardware safety limits. Errors never trigger alerts.");
        _alertMetric = new ComboBox
        {
            ItemsSource = MetricRegistry.All.Where(d => d.Unit is not "").ToArray(),
            DisplayMemberPath = "Label",
            SelectedValuePath = "Id"
        };
        SettingsPanelBuilder.Name(_alertMetric, "Alert metric");
        _alertMetric.SelectionChanged += (_, _) => SelectAlert();
        p.Panel.Children.Add(_alertMetric);
        var editor = new SettingsPanelBuilder("Threshold rule", new AlertRule());
        editor.Check("Enable this metric alert (continues while hidden)", "Enabled");
        editor.Choice("Comparison", "Comparison", new[] { ">=", "<=" });
        editor.Text("Warning threshold", "Warning");
        editor.Text("Critical threshold", "Critical");
        editor.Text("Clear threshold (hysteresis)", "Clear");
        editor.Text("Sustained duration (seconds)", "SustainedSeconds");
        editor.Text("Cooldown (seconds)", "CooldownSeconds");
        editor.Choice("Delivery channel", "DeliveryChannel", new[] { "Default", "Overlay only", "Tray only", "Overlay + Tray" });
        _alertEditor = editor.Panel;
        p.Panel.Children.Add(_alertEditor);
        _alertMetric.SelectedIndex = 0;
        p.Check("Tray balloon notifications", "TrayNotifications");
        p.Check("Alert sound (opt-in)", "AlertSound");
        p.Check("Suppress alerts during fullscreen", "SuppressAlertsFullscreen");
        p.Text("Quiet hours start (0–23; -1 off)", "QuietStartHour");
        p.Text("Quiet hours end (0–23; -1 off)", "QuietEndHour");
        p.Action("Dismiss alerts / snooze 15 minutes", (_, _) =>
        {
            if (_runtime == null) return;
            _runtime.Alerts.SnoozedUntil = DateTimeOffset.UtcNow.AddMinutes(15);
            _runtime.ActiveAlerts.Clear();
        });
    }

    private void SelectAlert()
    {
        if (_alertMetric.SelectedItem is not MetricDefinition metric) return;
        var settings = _draft;
        var rule = settings.Alerts.FirstOrDefault(a => a.MetricId == metric.Id);
        if (rule == null) { rule = new AlertRule { MetricId = metric.Id }; settings.Alerts.Add(rule); }
        _alertEditor.DataContext = rule;
    }

    private void BuildHistory(SettingsPanelBuilder p)
    {
        p.Choice("Rolling history", "HistorySeconds", new[] { new HistoryChoice(60, "1 minute"),
            new HistoryChoice(300, "5 minutes"), new HistoryChoice(900, "15 minutes"),
            new HistoryChoice(3600, "60 minutes") }, "Label", "Seconds");
        p.Check("Persist sampled history locally (opt-in; may include enabled process names)", "PersistHistory");
        p.Text("Local retention days (1–30)", "HistoryRetentionDays");
        p.Hint("Exports contain metric IDs, timestamps, units, quality and source metadata. Nothing is uploaded. " +
            "Unavailable periods remain gaps. History is bounded to 7200 samples per metric.");
        p.Action("Open history", (_, _) => OpenHistory());
        p.Action("Export CSV…", async (_, _) => await Export(false));
        p.Action("Export JSON…", async (_, _) => await Export(true));
        p.Action("Clear history…", (_, _) => ClearHistory());
    }

    private sealed record MonitorChoice(int Index, string Label);
    private sealed record HistoryChoice(int Seconds, string Label);
}
