using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace DiskOptimizer.Services;

public static class DiskHelper
{
    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    private const uint SHERB_NOCONFIRMATION = 0x00000001;
    private const uint SHERB_NOPROGRESSUI = 0x00000002;
    private const uint SHERB_NOSOUND = 0x00000004;

    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static bool IsCommandAvailable(string command)
    {
        try
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            var paths = pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries);
            var extensions = new[] { ".exe", ".cmd", ".bat", "" };
            foreach (var p in paths)
            {
                foreach (var ext in extensions)
                {
                    var full = Path.Combine(p.Trim(), command + ext);
                    if (File.Exists(full)) return true;
                }
            }
        }
        catch { }
        return false;
    }

    public static void RestartAsAdmin()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath)) return;

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = true,
            Verb = "runas"
        };

        try
        {
            Process.Start(startInfo);
            Environment.Exit(0);
        }
        catch
        {
            // Użytkownik odrzucił monit UAC
        }
    }

    public static (long sizeBytes, long itemCount) QueryRecycleBin(string? driveLetter = null)
    {
        try
        {
            var info = new SHQUERYRBINFO();
            info.cbSize = Marshal.SizeOf(typeof(SHQUERYRBINFO));
            string? rootPath = driveLetter != null ? $"{driveLetter}:\\" : null;
            int res = SHQueryRecycleBin(rootPath, ref info);
            if (res == 0)
            {
                return (info.i64Size, info.i64NumItems);
            }
        }
        catch
        {
            // Ignoruj błąd P/Invoke
        }
        return (0, 0);
    }

    public static bool EmptyRecycleBin(string? driveLetter = null)
    {
        try
        {
            string? rootPath = driveLetter != null ? $"{driveLetter}:\\" : null;
            uint flags = SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND;
            uint res = SHEmptyRecycleBin(IntPtr.Zero, rootPath, flags);
            return res == 0;
        }
        catch
        {
            return false;
        }
    }

    public static (long sizeBytes, int fileCount) GetDirectorySize(
        string directoryPath, CancellationToken ct = default, Action<string, Exception>? onSkipped = null)
    {
        ct.ThrowIfCancellationRequested();
        long totalSize = 0;
        int fileCount = 0;
        var stack = new Stack<string>();
        stack.Push(directoryPath);

        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            string currentDir = stack.Pop();
            try
            {
                var dirInfo = new DirectoryInfo(currentDir);
                // Junctions are intentionally outside this scan's scope.
                if ((dirInfo.Attributes & FileAttributes.ReparsePoint) != 0 && currentDir != directoryPath)
                    continue;

                foreach (var file in dirInfo.EnumerateFiles())
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        totalSize += file.Length;
                        fileCount++;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                    {
                        onSkipped?.Invoke(file.FullName, ex);
                    }
                }

                foreach (var subDir in dirInfo.EnumerateDirectories())
                {
                    ct.ThrowIfCancellationRequested();
                    if ((subDir.Attributes & FileAttributes.ReparsePoint) == 0)
                        stack.Push(subDir.FullName);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException)
            {
                onSkipped?.Invoke(currentDir, ex);
            }
        }

        ct.ThrowIfCancellationRequested();
        return (totalSize, fileCount);
    }

    public static long GetFileSize(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                return new FileInfo(filePath).Length;
            }
        }
        catch { }
        return 0;
    }

    // Reject destructive roots and links in every ancestor, not just the leaf.
    public static string ValidateCleaningPath(string path)
    {
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        string[] protectedRoots = { Path.GetPathRoot(full)!, Environment.GetFolderPath(Environment.SpecialFolder.Windows), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) };
        if (protectedRoots.Any(p => !string.IsNullOrWhiteSpace(p) && string.Equals(Path.TrimEndingDirectorySeparator(p), full, StringComparison.OrdinalIgnoreCase)))
            throw new IOException("Nie można czyścić głównego folderu systemu, użytkownika ani dysku.");
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string[] allowedWindowsCaches = { Path.Combine(windows, "Temp"), Path.Combine(windows, "SoftwareDistribution", "Download") };
        if (full.StartsWith(windows + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !allowedWindowsCaches.Any(cache => full.Equals(cache, StringComparison.OrdinalIgnoreCase) || full.StartsWith(cache + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            throw new IOException("Katalogi systemu Windows poza rozpoznanymi pamięciami podręcznymi są chronione.");
        for (DirectoryInfo? current = new DirectoryInfo(full); current != null; current = current.Parent)
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Czyszczenie przez dowiązanie jest zablokowane.");
        return full;
    }

    public static (long freedBytes, int deletedFiles) CleanDirectoryContents(
        string directoryPath, 
        Action<string>? logger = null, 
        int daysOlderThan = 0,
        CancellationToken ct = default)
    {
        if (!Directory.Exists(directoryPath)) return (0, 0);

        long freedBytes = 0;
        int deletedFiles = 0;
        directoryPath = ValidateCleaningPath(directoryPath);
        if (daysOlderThan < 0) throw new ArgumentOutOfRangeException(nameof(daysOlderThan));
        var thresholdDate = DateTime.Now.AddDays(-daysOlderThan);

        var stack = new Stack<string>();
        stack.Push(directoryPath);
        var subDirsToDelete = new List<string>();

        while (stack.Count > 0)
        {
            if (ct.IsCancellationRequested) break;
            string currentDir = stack.Pop();

            try
            {
                var dir = new DirectoryInfo(currentDir);

                // Pomiń punkty reparse
                if ((dir.Attributes & FileAttributes.ReparsePoint) != 0)
                    continue;

                foreach (var file in dir.EnumerateFiles())
                {
                    if (ct.IsCancellationRequested) break;
                    try
                    {
                        if ((file.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                        if (daysOlderThan > 0 && file.LastWriteTime > thresholdDate)
                            continue;

                        long len = file.Length;
                        if ((file.Attributes & FileAttributes.ReadOnly) != 0)
                        {
                            file.Attributes &= ~FileAttributes.ReadOnly;
                        }
                        file.Delete();
                        freedBytes += len;
                        deletedFiles++;
                    }
                    catch (Exception)
                    {
                        logger?.Invoke($"  [W użyciu] Pominięto aktywny plik: {file.Name}");
                    }
                }

                foreach (var subDir in dir.EnumerateDirectories())
                {
                    if ((subDir.Attributes & FileAttributes.ReparsePoint) == 0)
                    {
                        stack.Push(subDir.FullName);
                        subDirsToDelete.Add(subDir.FullName);
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.Invoke($"  Ostrzeżenie dostępu do {currentDir}: {ex.Message}");
            }
        }

        // Usuwanie pustych podfolderów (od najgłębszych)
        foreach (var subPath in subDirsToDelete.OrderByDescending(s => s.Length))
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                if (Directory.Exists(subPath) && !Directory.EnumerateFileSystemEntries(subPath).Any())
                {
                    Directory.Delete(subPath, false);
                }
            }
            catch { }
        }

        return (freedBytes, deletedFiles);
    }

    public static async Task<(bool success, string output)> RunProcessAsync(
        string fileName, 
        string arguments, 
        Action<string>? logger = null, 
        CancellationToken ct = default)
    {
        var (ok, output, _) = await RunProcessDetailedAsync(fileName, arguments, logger, ct).ConfigureAwait(false);
        return (ok, output);
    }

    public static async Task<(bool success, string output, int exitCode)> RunProcessDetailedAsync(
        string fileName, 
        string arguments, 
        Action<string>? logger = null, 
        CancellationToken ct = default)
    {
        var sb = new System.Text.StringBuilder();
        var errors = new System.Text.StringBuilder();
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    lock (sb) sb.AppendLine(e.Data);
                    logger?.Invoke(e.Data);
                }
            };
            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    lock (errors) errors.AppendLine(e.Data);
                    logger?.Invoke(e.Data);
                }
            };

            ct.ThrowIfCancellationRequested();
            process.Start();
            using var cancellation = ct.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
            });
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            string captured = sb.ToString().Trim();
            if (process.ExitCode != 0 && errors.Length > 0) captured += Environment.NewLine + errors.ToString().Trim();
            return (process.ExitCode == 0, string.IsNullOrEmpty(captured) ? $"Kod zakończenia: {process.ExitCode}" : captured, process.ExitCode);
        }
        catch (Exception ex)
        {
            logger?.Invoke($"Nie można uruchomić {fileName}: {ex.Message}");
            return (false, ex.Message, -1);
        }
    }

    public static async Task<(bool success, string output, int exitCode)> RunPowerShellScriptAsync(
        string script, 
        Action<string>? logger = null, 
        CancellationToken ct = default)
    {
        // PowerShell -EncodedCommand przyjmuje Base64 z ciągu Unicode (UTF-16LE)
        // Całkowicie eliminuje problemy ze znakami specjalnymi, polskimi literami i cudzysłowami
        script = "$ProgressPreference='SilentlyContinue'; $ErrorActionPreference='Stop'; [Console]::OutputEncoding=[System.Text.Encoding]::UTF8; " + script;
        byte[] bytes = System.Text.Encoding.Unicode.GetBytes(script);
        string encoded = Convert.ToBase64String(bytes);
        return await RunProcessDetailedAsync("powershell.exe", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}", logger, ct).ConfigureAwait(false);
    }
}
