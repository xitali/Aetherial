using System.IO;
using System.Text.Json;
using DiskOptimizer.Models;

namespace DiskOptimizer.Services;

public class SettingsService
{
    public static string SettingsFilePath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aetherial", "settings.json");
    private AppSettings _currentSettings = new();
    private readonly string _filePath;
    public AppSettings Current => _currentSettings;
    public string? LastError { get; private set; }

    public SettingsService() : this(SettingsFilePath) { }
    public SettingsService(string filePath)
    {
        _filePath = Path.GetFullPath(filePath);
        LoadSettings();
    }

    public void LoadSettings()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_filePath)) ?? throw new JsonException("Pusty plik ustawień.");
                loaded.Validate();
                _currentSettings = loaded;
            }
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
        string temporaryPath = _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            settings.Validate();
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporaryPath, _filePath, true);
            _currentSettings = settings;
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = $"Nie zapisano ustawień: {ex.Message}";
            throw new IOException(LastError, ex);
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
            catch (Exception cleanupError) { System.Diagnostics.Trace.TraceWarning(cleanupError.Message); }
        }
    }
}
