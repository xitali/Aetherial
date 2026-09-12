using System.IO;
using System.Runtime.InteropServices;
using DiskOptimizer.Models;

namespace DiskOptimizer.Services;

public class FileSystemExplorerService
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpszProgressTitle;
    }

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_SILENT = 0x0004;

    private static readonly HashSet<string> ProtectedRootFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "bootmgr", "BOOTNXT", "pagefile.sys", "swapfile.sys", "DumpStack.log.tmp", "hiberfil.sys", "bootsect.bak"
    };

    public static bool IsProtectedPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return true;

        string normalized = Path.GetFullPath(path).TrimEnd('\\', '/');
        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\', '/');
        string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).TrimEnd('\\', '/');
        string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData).TrimEnd('\\', '/');

        // 1. Katalogi systemu Windows (C:\Windows, System32 itp.)
        if (normalized.Equals(winDir, StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(winDir + @"\", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 2. Windows Defender i aplikacje systemowe
        if (normalized.StartsWith(Path.Combine(progFiles, "Windows Defender"), StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(Path.Combine(progFiles, "Windows Security"), StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(Path.Combine(progFiles, "WindowsApps"), StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(Path.Combine(progData, @"Microsoft\Windows Defender"), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 3. Specjalne foldery systemowe
        string fileName = Path.GetFileName(normalized);
        if (fileName.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("Recovery", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 4. Chronione pliki jądra i rozruchu na głównym dysku
        if (ProtectedRootFiles.Contains(fileName))
        {
            return true;
        }

        return false;
    }

    public static (string text, string fgColor, string bgColor) GetAgeBadge(DateTime dt)
    {
        if (dt == DateTime.MinValue)
            return ("Nieznany", "#888899", "#202028");

        var span = DateTime.Now - dt;
        if (span.TotalDays < 0)
            return ("Przed chwilą", "#81C784", "#1B3B24");

        if (span.TotalHours < 24 && dt.Date == DateTime.Today)
            return ("Dzisiaj", "#81C784", "#1B3B24");

        if (dt.Date == DateTime.Today.AddDays(-1))
            return ("Wczoraj", "#81C784", "#1B3B24");

        int days = (int)span.TotalDays;
        if (days < 7)
            return ($"{days} dni temu", "#81C784", "#1B3B24");

        if (days < 30)
        {
            int weeks = Math.Max(1, days / 7);
            string wStr = weeks == 1 ? "1 tydzień temu" : (weeks is >= 2 and <= 4 ? $"{weeks} tygodnie temu" : $"{weeks} tygodni temu");
            return (wStr, "#64B5F6", "#162E44");
        }

        if (days < 365)
        {
            int months = Math.Max(1, (int)(days / 30.43));
            string mStr = months == 1 ? "1 miesiąc temu" : (months is >= 2 and <= 4 ? $"{months} miesiące temu" : $"{months} miesięcy temu");
            return (mStr, "#FFB74D", "#3E2A14");
        }

        int years = Math.Max(1, (int)(days / 365.25));
        string yStr = years == 1 ? "ponad rok temu" : (years is >= 2 and <= 4 ? $"{years} lata temu" : $"{years} lat temu");
        return (yStr, "#E57373", "#3E1818");
    }

    public async Task<List<FileSystemItem>> GetFolderContentsAsync(string folderPath, CancellationToken ct = default)
    {
        var results = new List<FileSystemItem>();
        if (!Directory.Exists(folderPath)) return results;

        await Task.Run(() =>
        {
            try
            {
                var dirInfo = new DirectoryInfo(folderPath);
                string driveLetter = Path.GetPathRoot(folderPath)?.TrimEnd('\\', ':') ?? "C";

                // 1. Podfoldery
                foreach (var subDir in dirInfo.EnumerateDirectories())
                {
                    if (ct.IsCancellationRequested) break;
                    try
                    {
                        bool isProtected = IsProtectedPath(subDir.FullName);
                        var (text, fg, bg) = GetAgeBadge(subDir.LastWriteTime);

                        results.Add(new FileSystemItem
                        {
                            Path = subDir.FullName,
                            Name = subDir.Name,
                            ParentDirectory = folderPath,
                            DriveLetter = driveLetter,
                            IsDirectory = true,
                            LastModified = subDir.LastWriteTime,
                            RelativeAgeText = isProtected ? "🔒 Systemowy" : text,
                            AgeBadgeColor = isProtected ? "#FF8A80" : fg,
                            AgeBadgeBackground = isProtected ? "#2D1515" : bg,
                            IsSystemProtected = isProtected,
                            Status = isProtected ? "Chroniony" : "Folder"
                        });
                    }
                    catch { }
                }

                // 2. Pliki
                foreach (var file in dirInfo.EnumerateFiles())
                {
                    if (ct.IsCancellationRequested) break;
                    try
                    {
                        bool isProtected = IsProtectedPath(file.FullName);
                        var (text, fg, bg) = GetAgeBadge(file.LastWriteTime);

                        results.Add(new FileSystemItem
                        {
                            Path = file.FullName,
                            Name = file.Name,
                            ParentDirectory = folderPath,
                            DriveLetter = driveLetter,
                            IsDirectory = false,
                            SizeBytes = file.Length,
                            Extension = file.Extension,
                            LastModified = file.LastWriteTime,
                            RelativeAgeText = isProtected ? "🔒 Systemowy" : text,
                            AgeBadgeColor = isProtected ? "#FF8A80" : fg,
                            AgeBadgeBackground = isProtected ? "#2D1515" : bg,
                            IsSystemProtected = isProtected,
                            Status = isProtected ? "Chroniony" : "Plik"
                        });
                    }
                    catch { }
                }
            }
            catch (Exception)
            {
                // Błędy dostępu do katalogu
            }
        }, ct);

        return results;
    }

    public async Task<List<FileSystemItem>> FindLargeFilesAsync(string rootPath, long minSizeBytes = 500 * 1024 * 1024, int limit = 60, CancellationToken ct = default)
    {
        var results = new List<FileSystemItem>();
        if (!Directory.Exists(rootPath)) return results;

        await Task.Run(() =>
        {
            try
            {
                var stack = new Stack<string>();
                stack.Push(rootPath);
                string driveLetter = Path.GetPathRoot(rootPath)?.TrimEnd('\\', ':') ?? "C";

                while (stack.Count > 0)
                {
                    if (ct.IsCancellationRequested) break;
                    string current = stack.Pop();

                    try
                    {
                        var di = new DirectoryInfo(current);
                        if ((di.Attributes & FileAttributes.ReparsePoint) != 0 && current != rootPath)
                            continue;

                        // Pomiń folder Windows przy skanowaniu gigantów, aby nie usuwać plików systemowych
                        if (current.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows), StringComparison.OrdinalIgnoreCase))
                            continue;

                        foreach (var f in di.EnumerateFiles())
                        {
                            if (ct.IsCancellationRequested) break;
                            try
                            {
                                if (f.Length >= minSizeBytes)
                                {
                                    bool isProtected = IsProtectedPath(f.FullName);
                                    if (isProtected) continue; // Pomiń pliki chronione

                                    var (text, fg, bg) = GetAgeBadge(f.LastWriteTime);
                                    results.Add(new FileSystemItem
                                    {
                                        Path = f.FullName,
                                        Name = f.Name,
                                        ParentDirectory = f.DirectoryName ?? current,
                                        DriveLetter = driveLetter,
                                        IsDirectory = false,
                                        SizeBytes = f.Length,
                                        Extension = f.Extension,
                                        LastModified = f.LastWriteTime,
                                        RelativeAgeText = text,
                                        AgeBadgeColor = fg,
                                        AgeBadgeBackground = bg,
                                        IsSystemProtected = false,
                                        Status = "Wielki plik"
                                    });

                                    if (results.Count >= limit) return;
                                }
                            }
                            catch { }
                        }

                        foreach (var sub in di.EnumerateDirectories())
                        {
                            if ((sub.Attributes & FileAttributes.ReparsePoint) == 0 &&
                                !sub.FullName.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows), StringComparison.OrdinalIgnoreCase))
                            {
                                stack.Push(sub.FullName);
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

    public async Task<(long freedBytes, int deletedCount)> DeleteItemsAsync(
        IEnumerable<FileSystemItem> items, 
        bool moveToRecycleBin = true, 
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

                // TWARDA BLOKADA BEZPIECZEŃSTWA WINDOWS
                if (item.IsSystemProtected || IsProtectedPath(item.Path))
                {
                    logger?.Invoke($"🛡️ [BLOKADA BEZPIECZEŃSTWA] Pominięto kluczowy plik systemowy: {item.Path}");
                    continue;
                }

                try
                {
                    DiskHelper.ValidateCleaningPath(item.Path);
                    if (!File.Exists(item.Path) && !Directory.Exists(item.Path)) throw new FileNotFoundException("Element już nie istnieje.");
                    if ((File.GetAttributes(item.Path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Usuwanie dowiązań jest zablokowane.");
                    logger?.Invoke($"Usuwanie: {item.Path} (ostatnia edycja: {item.RelativeAgeText})...");

                    if (moveToRecycleBin)
                    {
                        bool ok = SendToRecycleBin(item.Path);
                        if (ok)
                        {
                            count++;
                            logger?.Invoke($"  ✓ Przeniesiono do Kosza: {item.Name} ({item.FormattedSize})");
                        }
                        else
                        {
                            logger?.Invoke($"Nie przeniesiono do Kosza: {item.Name}. Plik pozostawiono bez trwałego usuwania.");
                        }
                    }
                    else
                    {
                        long measuredSize = item.IsDirectory ? DiskHelper.GetDirectorySize(item.Path, ct).sizeBytes : new FileInfo(item.Path).Length;
                        DeletePermanently(item);
                        freed += measuredSize;
                        count++;
                        logger?.Invoke($"  ✓ Trwale usunięto: {item.Name} ({item.FormattedSize})");
                    }
                }
                catch (Exception ex)
                {
                    logger?.Invoke($"  ❌ Błąd usuwania {item.Name}: {ex.Message}");
                }
            }
        }, ct);

        return (freed, count);
    }

    private static void DeletePermanently(FileSystemItem item)
    {
        if (item.IsDirectory)
        {
            if (Directory.Exists(item.Path))
                Directory.Delete(item.Path, true);
        }
        else
        {
            if (File.Exists(item.Path))
            {
                var fi = new FileInfo(item.Path);
                fi.IsReadOnly = false;
                fi.Delete();
            }
        }
    }

    private static bool SendToRecycleBin(string path)
    {
        try
        {
            var fileOp = new SHFILEOPSTRUCT
            {
                wFunc = FO_DELETE,
                pFrom = path + '\0' + '\0',
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT
            };

            int res = SHFileOperation(ref fileOp);
            return res == 0 && !fileOp.fAnyOperationsAborted;
        }
        catch
        {
            return false;
        }
    }
}
