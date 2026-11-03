using System.IO;
using System.Text.Json;
using StatsInfo.Models;

namespace StatsInfo.Services;

public class SettingsService
{
    private readonly string _settingsPath;
    private AppSettings _currentSettings;

    public AppSettings Settings => _currentSettings;
    public string SettingsPath => _settingsPath;
    public string? LastError { get; private set; }
    public bool LastSaveSucceeded { get; private set; } = true;

    public event Action<AppSettings>? SettingsChanged;

    public SettingsService(string? settingsPath = null, string? applicationDataDirectory = null)
    {
        string folder = settingsPath == null
            ? Path.Combine(
                applicationDataDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "StatsInfo")
            : Path.GetDirectoryName(Path.GetFullPath(settingsPath))!;
        Directory.CreateDirectory(folder);
        _settingsPath = settingsPath == null ? Path.Combine(folder, "settings.json") : Path.GetFullPath(settingsPath);

        _currentSettings = LoadSettings();
    }

    public AppSettings LoadSettings()
    {
        if (File.Exists(_settingsPath))
        {
            try
            {
                if (new FileInfo(_settingsPath).Length > 4 * 1024 * 1024)
                    throw new JsonException("Settings exceed the 4 MiB size limit.");
                string json = File.ReadAllText(_settingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    MetricSettings.Normalize(loaded);
                    return loaded;
                }
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                LastError = $"Settings could not be read ({ex.GetType().Name}); original file retained.";
            }
        }

        var defaults = new AppSettings();
        MetricSettings.Normalize(defaults);
        return defaults;
    }

    public void SaveSettings(AppSettings settings)
    {
        _currentSettings = settings;
        MetricSettings.Normalize(settings);
        LastSaveSucceeded = false;
        try
        {
            if (File.Exists(_settingsPath) && !File.Exists(_settingsPath + ".pre-v2.bak"))
                File.Copy(_settingsPath, _settingsPath + ".pre-v2.bak");
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath + ".tmp", json);
            File.Move(_settingsPath + ".tmp", _settingsPath, true);
            LastSaveSucceeded = true;
            LastError = null;
            SettingsChanged?.Invoke(_currentSettings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            LastError = $"Settings could not be saved ({ex.GetType().Name}).";
        }
    }
}
