using System.IO;
using System.Text.Json;
using StatsInfo.Models;

namespace StatsInfo.Services;

public class SettingsService
{
    private readonly string _settingsPath;
    private AppSettings _currentSettings;

    public AppSettings Settings => _currentSettings;

    public event Action<AppSettings>? SettingsChanged;

    public SettingsService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string folder = Path.Combine(appData, "StatsInfo");
        Directory.CreateDirectory(folder);
        _settingsPath = Path.Combine(folder, "settings.json");

        _currentSettings = LoadSettings();
    }

    public AppSettings LoadSettings()
    {
        if (File.Exists(_settingsPath))
        {
            try
            {
                string json = File.ReadAllText(_settingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null) return loaded;
            }
            catch
            {
                // Fallback to default on corrupted file
            }
        }

        return new AppSettings();
    }

    public void SaveSettings(AppSettings settings)
    {
        _currentSettings = settings;
        try
        {
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
            SettingsChanged?.Invoke(_currentSettings);
        }
        catch
        {
            // Settings persistence failure handled gracefully
        }
    }
}
