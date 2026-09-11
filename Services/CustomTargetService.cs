using System.IO;
using System.Text.Json;
using DiskOptimizer.Models;

namespace DiskOptimizer.Services;

public class CustomTargetService
{
    private readonly string _configDirectory;
    private readonly string _configFilePath;

    public CustomTargetService()
    {
        _configDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DiskOptimizer");
        _configFilePath = Path.Combine(_configDirectory, "custom_targets.json");
    }

    public async Task<List<CustomTarget>> LoadAsync()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                string json = await File.ReadAllTextAsync(_configFilePath);
                var list = JsonSerializer.Deserialize<List<CustomTarget>>(json);
                if (list != null && list.Count > 0)
                {
                    return list;
                }
            }
        }
        catch { }

        // Domyślna propozycja: Folder Pobrane (starsze niż 30 dni) - odznaczony domyślnie
        string downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        var defaults = new List<CustomTarget>();
        if (Directory.Exists(downloadsPath))
        {
            defaults.Add(new CustomTarget
            {
                Path = downloadsPath,
                IsDirectory = true,
                DeleteMode = CustomDeleteMode.ContentsOnly,
                DaysOlderThan = 30,
                IsSelected = false
            });
        }
        return defaults;
    }

    public async Task SaveAsync(IEnumerable<CustomTarget> targets)
    {
        try
        {
            Directory.CreateDirectory(_configDirectory);
            string json = JsonSerializer.Serialize(targets.ToList(), new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_configFilePath, json);
        }
        catch { }
    }

    public async Task ScanTargetAsync(CustomTarget target, CancellationToken ct = default)
    {
        target.Status = "Trwa skanowanie...";

        try
        {
            await Task.Run(() =>
            {
                if (!target.IsDirectory && File.Exists(target.Path))
                {
                    var fi = new FileInfo(target.Path);
                    if (target.DaysOlderThan == 0 || fi.LastWriteTime < DateTime.Now.AddDays(-target.DaysOlderThan))
                    {
                        target.SizeBytes = fi.Length;
                        target.FileCount = 1;
                    }
                    else
                    {
                        target.SizeBytes = 0;
                        target.FileCount = 0;
                    }
                }
                else if (Directory.Exists(target.Path))
                {
                    long totalBytes = 0;
                    int count = 0;
                    var threshold = DateTime.Now.AddDays(-target.DaysOlderThan);

                    var stack = new Stack<string>();
                    stack.Push(target.Path);

                    while (stack.Count > 0)
                    {
                        if (ct.IsCancellationRequested) break;
                        string current = stack.Pop();
                        try
                        {
                            var di = new DirectoryInfo(current);
                            if ((di.Attributes & FileAttributes.ReparsePoint) != 0 && current != target.Path)
                                continue;

                            foreach (var f in di.EnumerateFiles())
                            {
                                if (ct.IsCancellationRequested) break;
                                try
                                {
                                    if (target.DaysOlderThan == 0 || f.LastWriteTime < threshold)
                                    {
                                        totalBytes += f.Length;
                                        count++;
                                    }
                                }
                                catch { }
                            }

                            foreach (var sub in di.EnumerateDirectories())
                            {
                                if ((sub.Attributes & FileAttributes.ReparsePoint) == 0)
                                    stack.Push(sub.FullName);
                            }
                        }
                        catch { }
                    }

                    target.SizeBytes = totalBytes;
                    target.FileCount = count;
                }
                else
                {
                    target.SizeBytes = 0;
                    target.FileCount = 0;
                    target.Status = "Ścieżka nie istnieje";
                    return;
                }
            }, ct);

            target.Status = target.SizeBytes > 0
                ? $"Wykryto {target.FormattedSize} ({target.FileCount} plików)"
                : "Brak pasujących plików (0 B)";
        }
        catch (Exception ex)
        {
            target.Status = $"Błąd: {ex.Message}";
        }
    }

    public async Task<(long freedBytes, int deletedFiles)> CleanTargetAsync(
        CustomTarget target, 
        Action<string>? logger = null, 
        CancellationToken ct = default)
    {
        target.Status = "Trwa usuwanie...";
        long freedBytes = 0;
        int deletedFiles = 0;

        try
        {
            logger?.Invoke($"▶ Czyszczenie własnego celu: {target.Path} ({target.FormattedMode})...");

            if (!target.IsDirectory && File.Exists(target.Path))
            {
                var fi = new FileInfo(target.Path);
                long len = fi.Length;
                fi.IsReadOnly = false;
                fi.Delete();
                freedBytes = len;
                deletedFiles = 1;
                logger?.Invoke($"  ✓ Usunięto plik: {target.Name} ({DriveModel.FormatBytes(freedBytes)})");
            }
            else if (Directory.Exists(target.Path))
            {
                if (target.DeleteMode == CustomDeleteMode.EntireTarget)
                {
                    var (fb, df) = await Task.Run(() => DiskHelper.CleanDirectoryContents(target.Path, logger, target.DaysOlderThan, ct), ct);
                    freedBytes = fb;
                    deletedFiles = df;

                    if (target.DaysOlderThan == 0)
                    {
                        try
                        {
                            Directory.Delete(target.Path, true);
                            logger?.Invoke($"  ✓ Usunięto cały folder: {target.Path}");
                        }
                        catch (Exception ex)
                        {
                            logger?.Invoke($"  ⚠️ Nie można usunąć głównego folderu: {ex.Message}");
                        }
                    }
                }
                else
                {
                    // Tylko zawartość
                    var (fb, df) = await Task.Run(() => DiskHelper.CleanDirectoryContents(target.Path, logger, target.DaysOlderThan, ct), ct);
                    freedBytes = fb;
                    deletedFiles = df;
                    logger?.Invoke($"  ✓ Wyczyszczono zawartość folderu {target.Name}: {DriveModel.FormatBytes(freedBytes)} ({deletedFiles} plików).");
                }
            }

            target.SizeBytes = Math.Max(0, target.SizeBytes - freedBytes);
            target.Status = $"Wyczyszczono {DriveModel.FormatBytes(freedBytes)}";
        }
        catch (Exception ex)
        {
            target.Status = $"Błąd: {ex.Message}";
            logger?.Invoke($"  ❌ Błąd czyszczenia {target.Path}: {ex.Message}");
        }

        return (freedBytes, deletedFiles);
    }
}
