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
    public string FormattedSize => IsJunction ? "Dowiązanie" : Status == "Gotowy" ? "Nie zmierzono" : DriveModel.FormatBytes(SizeBytes);
    public bool IsJunction { get; set; }
    public bool CanRelocate => Status != "Gotowy" && !IsJunction && Directory.Exists(SourcePath);
    public string Status { get; set; } = "Gotowy";
}

public class SymlinkService
{
    public List<SymlinkPreset> GetDefaultPresets()
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string destinationRoot = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed && !string.Equals(d.RootDirectory.FullName, Path.GetPathRoot(userProfile), StringComparison.OrdinalIgnoreCase)).OrderByDescending(d => d.AvailableFreeSpace).Select(d => d.RootDirectory.FullName).FirstOrDefault() ?? Path.GetPathRoot(userProfile)!;
        string videosPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

        var allPresets = new List<SymlinkPreset>
        {
            new SymlinkPreset
            {
                Id = "ollama_models",
                Title = "Modele Ollama LLM",
                Description = "Przenosi dane do wybranej lokalizacji i zachowuje dostęp przez dowiązanie NTFS.",
                SourcePath = Path.Combine(userProfile, @".ollama\models"),
                SuggestedDestPath = Path.Combine(destinationRoot, "AI_Models", "ollama"),
                Icon = "🦙"
            },
            new SymlinkPreset
            {
                Id = "huggingface_cache",
                Title = "Pamięć podręczna modeli HuggingFace",
                Description = "Przenosi dane do wybranej lokalizacji i zachowuje dostęp przez dowiązanie NTFS.",
                SourcePath = Path.Combine(userProfile, @".cache\huggingface"),
                SuggestedDestPath = Path.Combine(destinationRoot, "AI_Models", "huggingface"),
                Icon = "🤗"
            },
            new SymlinkPreset
            {
                Id = "nvidia_shadowplay",
                Title = "Nagrania gier NVIDIA ShadowPlay",
                Description = "Przenosi dane do wybranej lokalizacji i zachowuje dostęp przez dowiązanie NTFS.",
                SourcePath = Path.Combine(videosPath, "NVIDIA"),
                SuggestedDestPath = Path.Combine(destinationRoot, "NVIDIA_Videos"),
                Icon = "🎮"
            },
            new SymlinkPreset
            {
                Id = "lmstudio_cache",
                Title = "Środowisko LM Studio",
                Description = "Przenosi dane do wybranej lokalizacji i zachowuje dostęp przez dowiązanie NTFS.",
                SourcePath = Path.Combine(userProfile, ".lmstudio"),
                SuggestedDestPath = Path.Combine(destinationRoot, "AI_Models", "lmstudio"),
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
                var destination = di.ResolveLinkTarget(true);
                if (destination != null) preset.SuggestedDestPath = destination.FullName;
                preset.Status = "Dowiązanie aktywne";
                preset.SizeBytes = 0;
            }
            else
            {
                var (size, count) = DiskHelper.GetDirectorySize(preset.SourcePath, ct);
                preset.SizeBytes = size;
                preset.Status = $"Zajmuje na dysku {preset.FormattedSize} ({count} el.)";
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

        string? backup = null;
        try
        {
            sourceDir = DiskHelper.ValidateCleaningPath(sourceDir);
            destinationDir = DiskHelper.ValidateCleaningPath(destinationDir);
            if (sourceDir.IndexOfAny(new[] { '%', '!', '&', '^', '"', '\r', '\n' }) >= 0 || destinationDir.IndexOfAny(new[] { '%', '!', '&', '^', '"', '\r', '\n' }) >= 0)
                throw new IOException("Ścieżka zawiera znaki nieobsługiwane przez mklink.");
            if (sourceDir.Equals(destinationDir, StringComparison.OrdinalIgnoreCase) || destinationDir.StartsWith(sourceDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || sourceDir.StartsWith(destinationDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Lokalizacje źródłowa i docelowa nie mogą się pokrywać.");
            if (Directory.Exists(destinationDir) && Directory.EnumerateFileSystemEntries(destinationDir).Any())
                throw new IOException("Folder docelowy musi być pusty.");
            var pending = new Stack<string>(); pending.Push(sourceDir);
            while (pending.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                foreach (var entry in new DirectoryInfo(pending.Pop()).EnumerateFileSystemInfos())
                {
                    if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Źródło zawiera dowiązania; migracja wymaga ręcznego przeniesienia.");
                    if (entry is DirectoryInfo directory) pending.Push(directory.FullName);
                }
            }
            Directory.CreateDirectory(destinationDir);
            var (_, output, code) = await DiskHelper.RunProcessDetailedAsync("robocopy.exe", $"\"{sourceDir}\" \"{destinationDir}\" /E /COPY:DAT /DCOPY:DAT /R:1 /W:1 /XJ", logger, ct);
            if (code < 0 || code >= 8) throw new IOException($"Kopiowanie nie powiodło się ({code}): {output}");
            ct.ThrowIfCancellationRequested();
            backup = sourceDir + ".aetherial-backup-" + Guid.NewGuid().ToString("N");
            Directory.Move(sourceDir, backup);
            var (linked, linkOutput) = await DiskHelper.RunProcessAsync("cmd.exe", $"/d /c mklink /J \"{sourceDir}\" \"{destinationDir}\"", logger, ct);
            if (!linked || (new DirectoryInfo(sourceDir).Attributes & FileAttributes.ReparsePoint) == 0)
                throw new IOException($"Nie utworzono dowiązania: {linkOutput}");
            logger?.Invoke($"Przeniesiono folder. Kopia bezpieczeństwa pozostaje w {backup}. Sprawdź aplikacje przed jej ręcznym usunięciem; miejsce źródła nie zostało jeszcze zwolnione.");
            return true;
        }
        catch (Exception ex)
        {
            if (backup != null && Directory.Exists(backup) && !Directory.Exists(sourceDir))
            {
                try { Directory.Move(backup, sourceDir); }
                catch (Exception restoreError) { logger?.Invoke($"Kopia danych: {backup}. Przywrócenie nie powiodło się: {restoreError.Message}"); }
            }
            logger?.Invoke($"Błąd migracji: {ex.Message}");
            return false;
        }
    }
}
