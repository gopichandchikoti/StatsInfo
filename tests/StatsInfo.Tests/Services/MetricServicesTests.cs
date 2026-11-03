using System.IO;
using System.Text.Json;
using StatsInfo.Models;
using StatsInfo.Services;
using Xunit;

namespace StatsInfo.Tests.Services;

public class MetricServicesTests
{
    [Fact]
    public void Formatting_RespectsQualityUnitsAndCompositeDependencies()
    {
        var settings = new AppSettings { NetworkUnit = "MB/s" };
        var sample = MetricSample.Number("cpu.temperature.package", 50, "test");
        Assert.Equal("122 °F", MetricFormatter.Format(sample, new() { Unit = "°F" }, settings));
        Assert.Equal("122.00 °F", MetricFormatter.Format(sample, new() { Unit = "°F", Decimals = 2 }, settings));
        Assert.Equal("Stale", MetricFormatter.Format(sample with { Quality = SampleQuality.Stale }, new(), settings));
        Assert.Equal("Warming up", MetricFormatter.Format(sample with { Quality = SampleQuality.WarmingUp }, new(), settings));
        Assert.Equal("N/A", MetricFormatter.Format(sample with { Quality = SampleQuality.Error }, new(), settings));
        var network = MetricSample.Number("network.download.rate", 80, "test");
        Assert.Equal("10 MB/s", MetricFormatter.Format(network, new(), settings));
        Assert.Equal(1.073741824, MetricFormatter.ConvertValue(1, "GiB", "GB"), 9);
        Assert.Equal(1.5, MetricFormatter.ConvertValue(1500, "MHz", "GHz"));
        Assert.Equal(1500, MetricFormatter.ConvertValue(1.5, "GHz", "MHz"));
        var ram = MetricSample.Number("memory.physical.used", 8, "test", "8/16 GiB");
        Assert.Equal("8/16 GiB", MetricFormatter.Format(ram, new(), settings));
        Assert.Equal("N/A", MetricFormatter.Format(ram, new() { Unit = "%" }, settings, new Dictionary<string, MetricSample>()));
        var samples = new Dictionary<string, MetricSample>
        { ["memory.physical.total"] = MetricSample.Number("memory.physical.total", 16, "test") };
        Assert.Equal("50%", MetricFormatter.Format(ram, new() { Unit = "%" }, settings, samples));
        var structured = sample with { Value = null, Values = Enumerable.Range(0, 6).ToDictionary(i => $"Core {i}", i => (double)i) };
        Assert.Contains("(+2)", MetricFormatter.Format(structured, new(), settings));
        Assert.Equal(SampleQuality.Error, MetricSample.Number("cpu.usage.total", double.NaN, "test").Quality);
        Assert.False(MetricSample.State("display.hdr", "Off", "test").IsNumeric);
        Assert.NotEmpty(MetricRegistry.All[0].SupportedPresets);
        Assert.NotEmpty(MetricRegistry.All[0].Capability);
        Assert.NotEmpty(MetricRegistry.ById["process.top_cpu"].PrivacyLevel);
        Assert.Equal("Moderate", MetricRegistry.ById["process.top_cpu"].SamplingCost);
    }

    [Fact]
    public async Task HistoryExports_PreserveTimestampsMetadataAndQuality_AndOptInPersistence()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var history = new MetricHistory();
            history.Add(MetricSample.Number("cpu.usage.total", 42.123, "source,with\"quote"));
            history.Add(MetricSample.Missing("cpu.usage.total", "sensor disconnected"));
            string csv = Path.Combine(dir, "export.csv"), json = Path.Combine(dir, "export.json");
            await history.ExportAsync(csv, false);
            await history.ExportAsync(json, true);
            Assert.Contains("source,with\"\"quote", File.ReadAllText(csv));
            using var doc = JsonDocument.Parse(File.ReadAllText(json));
            Assert.Equal(2, doc.RootElement.GetArrayLength());
            Assert.Equal("unavailable", doc.RootElement[1].GetProperty("Quality").GetString());
            var store = new HistoryStore(Path.Combine(dir, "settings.json"));
            var settings = new AppSettings();
            store.Save(history, settings, true);
            Assert.False(Directory.Exists(Path.Combine(dir, "history")));
            settings.PersistHistory = true;
            store.Save(history, settings, true);
            store.Save(history, settings);
            var restored = new MetricHistory();
            store.Restore(restored, settings);
            Assert.Equal(2, restored.GetAll().Length);
            Assert.Null(store.Error);
            string persisted = Directory.GetFiles(Path.Combine(dir, "history")).Single();
            using (var locked = new FileStream(persisted, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                store.Clear();
                Assert.Contains("could not be cleared", store.Error);
            }
            store.Clear();
            Assert.Empty(Directory.GetFiles(Path.Combine(dir, "history")));
            File.WriteAllText(persisted, "[null,{}, {\"Id\":\"unknown\"}]");
            var corruptEntries = new MetricHistory();
            store.Restore(corruptEntries, settings);
            Assert.Empty(corruptEntries.GetAll());
        }
        finally { Directory.Delete(dir, true); }
    }

    [Theory]
    [InlineData("Ctrl+Shift+F10", true)]
    [InlineData("Ctrl+Shift+F12", false)]
    [InlineData("Alt+F1", true)]
    [InlineData("F12", false)]
    [InlineData("Ctrl+F25", false)]
    [InlineData("Win+F12", false)]
    [InlineData("Ctrl+Ctrl+F1", false)]
    [InlineData("Ctrl+X", false)]
    public void HotkeyParser_RequiresSupportedUnambiguousCombinations(string text, bool valid) =>
        Assert.Equal(valid, SettingsValidation.TryHotkey(text, out _, out _));

    [Fact]
    public void Validation_RejectsInvalidEndpointsColorsAndAlertThresholds()
    {
        var s = new AppSettings();
        Assert.Null(SettingsValidation.Validate(s));
        s.LatencyEnabled = true;
        Assert.Contains("hostname", SettingsValidation.Validate(s));
        s.LatencyEnabled = false; s.ToggleHotkey = "F1";
        Assert.Contains("Hotkeys", SettingsValidation.Validate(s));
        s.ToggleHotkey = s.RecoveryHotkey;
        Assert.Contains("differ", SettingsValidation.Validate(s));
        s.ToggleHotkey = "Ctrl+Shift+F11"; s.QuietEndHour = 25;
        Assert.Contains("Quiet", SettingsValidation.Validate(s));
        s.QuietEndHour = -1; s.AccentColor = "not-a-color";
        Assert.Contains("color", SettingsValidation.Validate(s));
        s.AccentColor = "#ffffff";
        s.Alerts.Add(new() { Enabled = true, Warning = double.NaN });
        Assert.Contains("finite", SettingsValidation.Validate(s));
        s.Alerts[0].Warning = 90; s.Alerts[0].Clear = 100;
        Assert.Contains("clear", SettingsValidation.Validate(s));
        s.Alerts[0].Clear = 85;
        Assert.Null(SettingsValidation.Validate(s));
        s.Alerts[0].Comparison = "<="; s.Alerts[0].Warning = 30; s.Alerts[0].Critical = 20; s.Alerts[0].Clear = 40;
        Assert.Null(SettingsValidation.Validate(s));
    }
}
