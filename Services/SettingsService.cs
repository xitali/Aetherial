using System.IO;
using System.Text.Json;
using DiskOptimizer.Models;

namespace DiskOptimizer.Services;

public class SettingsService
{
    private static readonly string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
    private AppSettings _currentSettings = new();

    public AppSettings Current => _currentSettings;

    public SettingsService()
    {
        LoadSettings();
    }

    public void LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    _currentSettings = loaded;
                }
            }
        }
        catch { }

        // Upewnij się, że domyślny folder instalacji istnieje lub można go utworzyć
        try
        {
            if (!string.IsNullOrEmpty(_currentSettings.DefaultInstallFolder) && !Directory.Exists(_currentSettings.DefaultInstallFolder))
            {
                Directory.CreateDirectory(_currentSettings.DefaultInstallFolder);
            }
        }
        catch { }
    }

    public void SaveSettings(AppSettings settings)
    {
        try
        {
            _currentSettings = settings;
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch { }
    }
}
