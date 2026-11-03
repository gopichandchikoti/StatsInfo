using System.IO;
using StatsInfo.Models;
using StatsInfo.Services;
using Xunit;

namespace StatsInfo.Tests.Services;

public class MetricSettingsTests
{
    [Fact]
    public void Catalog_HasEverySpecMetric_WithUniqueStableIds()
    {
        Assert.Equal(70, MetricRegistry.All.Count);
        Assert.Equal(70, MetricRegistry.ById.Count);
        Assert.All(MetricRegistry.All, d => Assert.False(string.IsNullOrWhiteSpace(d.Description)));
    }

    [Fact]
    public void LegacyPreferences_MigrateWithoutResettingOrEnablingNewMetrics()
    {
        var s = new AppSettings
        {
            ShowCpuUsage = false,
            ShowNetwork = false,
            ShowGpuTemp = false,
            Opacity = 0.6,
            Position = "Custom",
            CustomX = -400,
            TargetMonitorIndex = 2
        };
        MetricSettings.Normalize(s);
        Assert.False(s.Metrics["cpu.usage.total"].Visible);
        Assert.False(s.Metrics["gpu.temperature.core"].Visible);
        Assert.False(s.Metrics["network.download.rate"].Visible);
        Assert.False(s.Metrics["network.upload.rate"].Visible);
        Assert.False(s.Metrics["cpu.power.package"].Visible);
        Assert.Equal(-400, s.CustomX);
        Assert.Equal(2, s.TargetMonitorIndex);
        Assert.Equal(0.6, s.Opacity);
        Assert.Equal(2, s.SchemaVersion);
    }

    [Fact]
    public void EveryMetric_TogglesAndOrdersPersist_AndUnknownSettingsSurvive()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "settings.json");
            File.WriteAllText(path, "{\"ShowCpuUsage\":false,\"FutureOption\":17}");
            var service = new SettingsService(path);
            foreach (var d in MetricRegistry.All)
            {
                MetricSettings.SetVisible(service.Settings, d.Id, true);
                service.Settings.Metrics[d.Id].Order = 70 - d.SortOrder;
            }
            service.SaveSettings(service.Settings);
            var loaded = new SettingsService(path).Settings;
            Assert.All(loaded.Metrics.Values, p => Assert.True(p.Visible));
            Assert.Equal(70, loaded.Metrics["cpu.usage.total"].Order);
            Assert.Contains("FutureOption", File.ReadAllText(path));
            Assert.Contains("false", File.ReadAllText(path + ".pre-v2.bak"));
            foreach (var d in MetricRegistry.All) MetricSettings.SetVisible(loaded, d.Id, false);
            service.SaveSettings(loaded);
            Assert.All(new SettingsService(path).Settings.Metrics.Values, p => Assert.False(p.Visible));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Presets_SaveCustomVisibility_WithoutChangingOrderAppearanceOrMonitor()
    {
        var s = new AppSettings { Opacity = 0.7, TargetMonitorIndex = 2 };
        MetricSettings.Normalize(s);
        MetricSettings.SetVisible(s, "cpu.usage.total", false);
        s.Metrics["gpu.power.board"].Order = -1;
        MetricSettings.ApplyPreset(s, "Gaming");
        Assert.True(s.Metrics["game.fps"].Visible);
        Assert.Equal(0.7, s.Opacity);
        Assert.Equal(-1, s.Metrics["gpu.power.board"].Order);
        MetricSettings.ApplyPreset(s, "Custom");
        Assert.False(s.Metrics["cpu.usage.total"].Visible);
        Assert.False(s.Metrics["game.fps"].Visible);
    }
}
