using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using DiskOptimizer.Models;

namespace DiskOptimizer.Services;

public class DevArtifactItem : INotifyPropertyChanged
{
    private bool _isSelected = false;

    public string ProjectName { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public string ArtifactType { get; set; } = "node_modules";
    public string FullPath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime ProjectLastModified { get; set; }
    public string RelativeAgeText { get; set; } = string.Empty;
    public string FormattedSize => DriveModel.FormatBytes(SizeBytes);

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public string TypeIcon => ArtifactType switch
    {
        "node_modules" => "📦",
        ".venv" or "venv" => "🐍",
        "bin" or "obj" => "🔷",
        "target" => "🦀",
        _ => "📁"
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class DevProjectsService
{
    private static readonly HashSet<string> ArtifactNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", ".venv", "venv", "bin", "obj", "target"
    };

    private static bool IsVerifiedArtifact(DirectoryInfo project, string name) => name.ToLowerInvariant() switch
    {
        "node_modules" => File.Exists(Path.Combine(project.FullName, "package.json")),
        ".venv" or "venv" => File.Exists(Path.Combine(project.FullName, name, "pyvenv.cfg")),
        "bin" or "obj" => project.EnumerateFiles("*.csproj").Any() || project.EnumerateFiles("*.fsproj").Any() || project.EnumerateFiles("*.vbproj").Any(),
        "target" => File.Exists(Path.Combine(project.FullName, "Cargo.toml")),
        _ => false
    };

    public async Task<List<DevArtifactItem>> ScanDevProjectsAsync(
        string rootDirectory, 
        int minDaysInactive = 0, 
        CancellationToken ct = default)
    {
        var results = new List<DevArtifactItem>();
        if (!Directory.Exists(rootDirectory)) return results;

        await Task.Run(() =>
        {
            try
            {
                var threshold = DateTime.Now.AddDays(-minDaysInactive);
                var stack = new Stack<string>();
                stack.Push(rootDirectory);

                while (stack.Count > 0)
                {
                    if (ct.IsCancellationRequested) break;
                    string current = stack.Pop();

                    try
                    {
                        var di = new DirectoryInfo(current);
                        if ((di.Attributes & FileAttributes.ReparsePoint) != 0 && current != rootDirectory)
                            continue;

                        // Pomiń foldery .git
                        if (di.Name.Equals(".git", StringComparison.OrdinalIgnoreCase))
                            continue;

                        foreach (var subDir in di.EnumerateDirectories())
                        {
                            if (ct.IsCancellationRequested) break;

                            if ((subDir.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                            if (ArtifactNames.Contains(subDir.Name) && IsVerifiedArtifact(di, subDir.Name))
                            {
                                // To jest folder artefaktu (np. node_modules, .venv)
                                if (minDaysInactive == 0 || di.LastWriteTime <= threshold)
                                {
                                    var (size, _) = DiskHelper.GetDirectorySize(subDir.FullName, ct);
                                    if (size > 0)
                                    {
                                        var (ageText, _, _) = FileSystemExplorerService.GetAgeBadge(di.LastWriteTime);
                                        results.Add(new DevArtifactItem
                                        {
                                            ProjectName = di.Name,
                                            ProjectPath = current,
                                            ArtifactType = subDir.Name,
                                            FullPath = subDir.FullName,
                                            SizeBytes = size,
                                            ProjectLastModified = di.LastWriteTime,
                                            RelativeAgeText = ageText,
                                            IsSelected = false
                                        });
                                    }
                                }
                            }
                            else
                            {
                                // Zwykły folder - sprawdzaj dalej w głąb
                                stack.Push(subDir.FullName);
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }, ct);

        return results.OrderByDescending(r => r.SizeBytes).ToList();
    }

    public async Task<(long freedBytes, int deletedCount)> CleanArtifactsAsync(
        IEnumerable<DevArtifactItem> items, 
        Action<string>? logger = null, 
        CancellationToken ct = default)
    {
        long freed = 0;
        int count = 0;

        await Task.Run(() =>
        {
            foreach (var item in items)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    logger?.Invoke($"Usuwanie artefaktu {item.ArtifactType} z projektu '{item.ProjectName}'...");
                    if (Directory.Exists(item.FullPath))
                    {
                        var artifact = new DirectoryInfo(DiskHelper.ValidateCleaningPath(item.FullPath));
                        if (artifact.Parent == null || !IsVerifiedArtifact(artifact.Parent, artifact.Name)) throw new IOException("Nie potwierdzono rodzaju artefaktu.");
                        var (fBytes, _) = DiskHelper.CleanDirectoryContents(item.FullPath, null, 0, ct);
                        freed += fBytes;
                        ct.ThrowIfCancellationRequested();
                        Directory.Delete(item.FullPath, false);
                        count++;
                        logger?.Invoke($"  ✓ Usunięto {item.ArtifactType} ({item.FormattedSize}). Kod źródłowy projektu pozostał nienaruszony!");
                    }
                }
                catch (Exception ex)
                {
                    logger?.Invoke($"  ❌ Błąd usuwania {item.FullPath}: {ex.Message}");
                }
            }
        }, ct);

        return (freed, count);
    }
}
