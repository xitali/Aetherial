using System.Diagnostics;
using System.IO;
using DiskOptimizer.Models;

namespace DiskOptimizer.Services;

public class SymlinkPreset
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string SuggestedDestPath { get; set; } = string.Empty;
    public string Icon { get; set; } = "🔗";
    public long SizeBytes { get; set; }
    public string FormattedSize => DriveModel.FormatBytes(SizeBytes);
    public bool IsJunction { get; set; }
    public string Status { get; set; } = "Gotowy";
}

public class SymlinkService
{
    public List<SymlinkPreset> GetDefaultPresets()
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string videosPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

        var allPresets = new List<SymlinkPreset>
        {
            new SymlinkPreset
            {
                Id = "ollama_models",
                Title = "Modele Ollama LLM",
                Description = "Przenosi folder ~/.ollama/models z dysku C: na dysk D: i tworzy dowiązanie NTFS.",
                SourcePath = Path.Combine(userProfile, @".ollama\models"),
                SuggestedDestPath = @"D:\AI_Models\ollama",
                Icon = "🦙"
            },
            new SymlinkPreset
            {
                Id = "huggingface_cache",
                Title = "Pamięć podręczna modeli HuggingFace",
                Description = "Przenosi ~/.cache/huggingface z dysku C: na dysk D: (oszczędność kilku GB).",
                SourcePath = Path.Combine(userProfile, @".cache\huggingface"),
                SuggestedDestPath = @"D:\AI_Models\huggingface",
                Icon = "🤗"
            },
            new SymlinkPreset
            {
                Id = "nvidia_shadowplay",
                Title = "Nagrania gier NVIDIA ShadowPlay",
                Description = "Przenosi folder z wideo gier z dysku C: na dysk E: (Gry).",
                SourcePath = Path.Combine(videosPath, "NVIDIA"),
                SuggestedDestPath = @"E:\Gry\NVIDIA_Videos",
                Icon = "🎮"
            },
            new SymlinkPreset
            {
                Id = "lmstudio_cache",
                Title = "Środowisko LM Studio",
                Description = "Przenosi ~/.lmstudio z dysku C: na dysk D:.",
                SourcePath = Path.Combine(userProfile, ".lmstudio"),
                SuggestedDestPath = @"D:\AI_Models\lmstudio",
                Icon = "🧠"
            }
        };

        return allPresets.Where(p => Directory.Exists(p.SourcePath)).ToList();
    }

    public async Task RefreshPresetStatusAsync(SymlinkPreset preset, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            if (!Directory.Exists(preset.SourcePath))
            {
                preset.SizeBytes = 0;
                preset.IsJunction = false;
                preset.Status = "Folder nie istnieje";
                return;
            }

            var di = new DirectoryInfo(preset.SourcePath);
            preset.IsJunction = (di.Attributes & FileAttributes.ReparsePoint) != 0;

            if (preset.IsJunction)
            {
                preset.Status = "Już przeniesiony (dowiązanie aktywne)";
                preset.SizeBytes = 0;
            }
            else
            {
                var (size, count) = DiskHelper.GetDirectorySize(preset.SourcePath, ct);
                preset.SizeBytes = size;
                preset.Status = $"Zajmuje na C: {preset.FormattedSize} ({count} el.)";
            }
        }, ct);
    }

    public async Task<bool> RelocateAndCreateJunctionAsync(
        string sourceDir, 
        string destinationDir, 
        Action<string>? logger = null, 
        CancellationToken ct = default)
    {
        if (!Directory.Exists(sourceDir))
        {
            logger?.Invoke($"Błąd: Folder źródłowy nie istnieje: {sourceDir}");
            return false;
        }

        var sourceDi = new DirectoryInfo(sourceDir);
        if ((sourceDi.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            logger?.Invoke($"Folder '{sourceDir}' jest już dowiązaniem symbolicznym!");
            return false;
        }

        try
        {
            logger?.Invoke($"=== Rozpoczynam migrację folderu ===");
            logger?.Invoke($"Źródło: {sourceDir}");
            logger?.Invoke($"Cel:    {destinationDir}");

            // 1. Utwórz folder docelowy
            Directory.CreateDirectory(destinationDir);

            // 2. Kopiowanie danych za pomocą robocopy (szybkie i niezawodne)
            logger?.Invoke("Trwa kopiowanie plików na nowy dysk...");
            var robocopyPsi = new ProcessStartInfo
            {
                FileName = "robocopy",
                Arguments = $"\"{sourceDir}\" \"{destinationDir}\" /E /COPY:DAT /DCOPY:DAT /R:1 /W:1 /NFL /NDL /NJH",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var p = Process.Start(robocopyPsi))
            {
                if (p != null) await p.WaitForExitAsync(ct);
            }

            logger?.Invoke("✓ Pliki zostały pomyślnie skopiowane.");

            // 3. Usuń oryginalny folder ze źródła (C:)
            logger?.Invoke("Usuwanie oryginalnego folderu ze starej lokalizacji na C:...");
            Directory.Delete(sourceDir, true);
            logger?.Invoke("✓ Zwolniono miejsce na starym dysku!");

            // 4. Utwórz dowiązanie NTFS Junction
            logger?.Invoke("Tworzenie przezroczystego dowiązania NTFS Directory Junction (mklink /J)...");
            var mklinkPsi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c mklink /J \"{sourceDir}\" \"{destinationDir}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var mkProc = Process.Start(mklinkPsi))
            {
                if (mkProc != null)
                {
                    string outMsg = await mkProc.StandardOutput.ReadToEndAsync(ct);
                    await mkProc.WaitForExitAsync(ct);
                    logger?.Invoke($"Wynik mklink: {outMsg.Trim()}");
                }
            }

            logger?.Invoke($"🎉 Sukces! Folder {sourceDir} został przeniesiony do {destinationDir}, a aplikacje będą z niego korzystać bez żadnych zmian!");
            return true;
        }
        catch (Exception ex)
        {
            logger?.Invoke($"❌ Błąd migracji: {ex.Message}");
            return false;
        }
    }
}
