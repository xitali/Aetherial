using System.IO;
using System.Text.Json;
using DiskOptimizer.Models;

namespace DiskOptimizer.Services;

public class SettingsService
{
    public static string SettingsFilePath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aetherial", "settings.json");
    private AppSettings _currentSettings = new();
    public AppSettings Current => _currentSettings;
    public string? LastError { get; private set; }

    public SettingsService() => LoadSettings();

    public void LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
                _currentSettings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFilePath)) ?? throw new JsonException("Pusty plik ustawień.");
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = $"Nie odczytano ustawień: {ex.Message}";
            System.Diagnostics.Trace.TraceError(LastError);
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);
        string temporaryPath = SettingsFilePath + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporaryPath, SettingsFilePath, true);
            _currentSettings = settings;
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = $"Nie zapisano ustawień: {ex.Message}";
            throw new IOException(LastError, ex);
        }
    }
}
