using System.IO;
using StatsInfo.Models;
using StatsInfo.Services;
using Xunit;

namespace StatsInfo.Tests.Services;

public class SettingsServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private string SettingsPath => Path.Combine(_directory, "settings.json");

    [Fact]
    public void Constructor_UsesDefaults_WhenSettingsFileDoesNotExist()
    {
        var service = new SettingsService(SettingsPath);

        Assert.Equal(0.85, service.Settings.Opacity);
        Assert.Equal(1000, service.Settings.UpdateIntervalMs);
    }

    [Fact]
    public void Constructor_UsesProvidedApplicationDataDirectory()
    {
        var service = new SettingsService(applicationDataDirectory: _directory);

        Assert.Equal(0.85, service.Settings.Opacity);
        Assert.True(Directory.Exists(Path.Combine(_directory, "StatsInfo")));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    public void LoadSettings_UsesDefaults_WhenFileIsInvalidOrNull(string json)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, json);
        var service = new SettingsService(SettingsPath);

        Assert.Equal(0.85, service.LoadSettings().Opacity);
    }

    [Fact]
    public void SaveSettings_PersistsSettings_AndRaisesChangedEvent()
    {
        var service = new SettingsService(SettingsPath);
        var settings = new AppSettings { Opacity = 0.6, UpdateIntervalMs = 2500 };
        AppSettings? changed = null;
        service.SettingsChanged += value => changed = value;

        service.SaveSettings(settings);

        Assert.Same(settings, changed);
        Assert.Same(settings, service.Settings);
        var loaded = new SettingsService(SettingsPath);
        Assert.Equal(0.6, loaded.Settings.Opacity);
        Assert.Equal(2500, loaded.Settings.UpdateIntervalMs);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
