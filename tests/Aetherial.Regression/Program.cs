using System.IO;
using DiskOptimizer.Models;
using DiskOptimizer.Services;

internal static class Program
{
    private static int passed;
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "AetherialRegression-" + Guid.NewGuid().ToString("N"));

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Console.WriteLine("PASS " + message);
        passed++;
    }

    private static void Reject(Action action, string message)
    {
        try { action(); }
        catch (IOException) { Check(true, message); return; }
        throw new InvalidOperationException("Not rejected: " + message);
    }

    private static string Fixture(string relative, int bytes = 128)
    {
        string path = Path.Combine(Root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
        return path;
    }

    public static async Task<int> Main()
    {
        Directory.CreateDirectory(Root);
        try
        {
            Reject(() => DiskHelper.ValidateCleaningPath(Path.GetPathRoot(Root)!), "Drive root protected");
            Reject(() => DiskHelper.ValidateCleaningPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)), "User root protected");
            Reject(() => DiskHelper.ValidateCleaningPath(Environment.GetFolderPath(Environment.SpecialFolder.Windows)), "Windows root protected");
            Reject(() => DiskHelper.ValidateCleaningPath(Environment.SystemDirectory), "Windows system subdirectory protected");

            string old = Fixture("age/old.bin", 113);
            File.SetLastWriteTime(old, DateTime.Now.AddDays(-60));
            string recent = Fixture("age/recent.bin", 227);
            var result = DiskHelper.CleanDirectoryContents(Path.GetDirectoryName(old)!, daysOlderThan: 30);
            Check(!File.Exists(old) && File.Exists(recent) && result == (113L, 1), "Age filter preserves recent files and reports actual deleted bytes");

            string locked = Fixture("locked/in-use.bin");
            using (var handle = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                result = DiskHelper.CleanDirectoryContents(Path.GetDirectoryName(locked)!);
                Check(result == (0L, 0) && File.Exists(locked), "Locked file is preserved without claiming recovered bytes");
            }

            string cancelled = Fixture("cancelled/keep.bin");
            result = DiskHelper.CleanDirectoryContents(Path.GetDirectoryName(cancelled)!, ct: new CancellationToken(true));
            Check(result == (0L, 0) && File.Exists(cancelled), "Cancellation preserves fixture");

            var custom = new CustomTargetService();
            string single = Fixture("custom/recent.bin");
            var singleTarget = new CustomTarget { Path = single, IsDirectory = false, DaysOlderThan = 30 };
            result = await custom.CleanTargetAsync(singleTarget);
            Check(result == (0L, 0) && File.Exists(single), "Custom single-file age filter is enforced at deletion");
            File.SetLastWriteTime(single, DateTime.Now.AddDays(-60));
            result = await custom.CleanTargetAsync(singleTarget);
            Check(result == (128L, 1) && !File.Exists(single), "Eligible single-file deletion reports actual bytes");

            string protectedFile = Fixture("custom-folder/recent.bin");
            result = await custom.CleanTargetAsync(new CustomTarget { Path = Path.GetDirectoryName(protectedFile)!, IsDirectory = true, DeleteMode = CustomDeleteMode.EntireTarget, DaysOlderThan = 30 });
            Check(result == (0L, 0) && File.Exists(protectedFile), "EntireTarget does not bypass age rules with recursive fallback");

            Fixture("projects/random/bin/important.bin");
            Fixture("projects/dotnet/sample.csproj", 0);
            Fixture("projects/dotnet/bin/build.bin");
            Fixture("projects/node/package.json", 0);
            Fixture("projects/node/node_modules/module.bin");
            Fixture("projects/rust/Cargo.toml", 0);
            Fixture("projects/rust/target/build.bin");
            var artifacts = await new DevProjectsService().ScanDevProjectsAsync(Path.Combine(Root, "projects"));
            Check(artifacts.Count == 3 && artifacts.All(x => !x.FullPath.Contains("random")), "Dev scanner requires matching project manifests");
            Check(artifacts.All(x => !x.IsSelected), "Dev artifacts require explicit selection");

            string source = Path.GetDirectoryName(Fixture("migration/source/file.bin"))!;
            var symlink = new SymlinkService();
            Check(!await symlink.RelocateAndCreateJunctionAsync(source, source), "Migration rejects identical source and destination");
            Check(!await symlink.RelocateAndCreateJunctionAsync(source, Path.Combine(source, "nested")), "Migration rejects nested destination");
            Check(File.Exists(Path.Combine(source, "file.bin")), "Rejected migration preserves source");
            string explorerFile = Fixture("explorer/delete.bin", 137);
            var explorerResult = await new FileSystemExplorerService().DeleteItemsAsync(new[] { new FileSystemItem { Path = explorerFile, IsDirectory = false, SizeBytes = 999999 } }, moveToRecycleBin: false);
            Check(explorerResult == (137L, 1) && !File.Exists(explorerFile), "Explorer measures current bytes instead of stale scan size");

            var drive = new DriveModel { TotalBytes = 100, FreeBytes = 30 };
            Check(drive.UsedBytes == 70 && drive.UsedPercent == 70, "Drive usage calculated from capacity and free space");
            Check(!string.IsNullOrWhiteSpace(DriveModel.FormatBytes(long.MaxValue)), "Byte formatter supports large capacities");
            Check(!new PnpDeviceItem().UpdateStatusBadge.Contains("Aktualny"), "Unknown driver version does not claim latest status");
            Check(new AppSettings().Theme == "Dark" && !new AppSettings().AutoCleanTempOnLaunch, "Safe default settings");

            var jsonProcess = await DiskHelper.RunPowerShellScriptAsync("[Console]::Error.WriteLine('diagnostic warning'); '{\"value\":42}'");
            Check(jsonProcess.success && System.Text.Json.JsonDocument.Parse(jsonProcess.output).RootElement.GetProperty("value").GetInt32() == 42, "Process diagnostics cannot corrupt JSON stdout");
            var failedProcess = await DiskHelper.RunPowerShellScriptAsync("throw 'regression expected failure'");
            Check(!failedProcess.success && failedProcess.exitCode != 0, "PowerShell errors do not report successful execution");

            Console.WriteLine($"{passed} regression checks passed; no real user data modified.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            string tempRoot = Path.GetFullPath(Path.GetTempPath());
            if (!Path.GetFullPath(Root).StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(Root).StartsWith("AetherialRegression-")) throw new IOException("Invalid fixture cleanup path");
            Directory.Delete(Root, true);
        }
    }
}
