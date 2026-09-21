using DiskOptimizer.Models;
using DiskOptimizer.Services;

internal static class SoftwareInventoryCases
{
    public static void Run(Action<bool, string> check)
    {
        static string Row(string name, string id, string version, string available, string source) =>
            $"{name,-26}{id,-44}{version,-18}{available,-18}{source}";
        string heading = Row("Name", "Id", "Version", "Available", "Source") + "\n" + new string('-', 114) + "\n";
        var parsed = SoftwareInventoryParser.Parse("\u001b[?25l\r" + heading +
            Row("Git", "Git.Git", "2.48.1", "2.49.0", "winget") + "\n" +
            Row("GitHub Desktop", "GitHub.GitHubDesktop", "3.4.1", "", "winget") + "\n" +
            Row("Local tool", "ARP\\User\\X64\\Local Tool", "1.0", "", "") + "\n");
        check(parsed.HasTable && parsed.RejectedRows == 0 && parsed.Apps.Count == 3,
            "WinGet inventory parses exact columns and strips terminal escapes");
        var compactColumns = SoftwareInventoryParser.Parse($"{"Name",-26}{"Id",-44}{"Version",-18}Available Source\n" +
            new string('-', 104) + "\n" + $"{"Git",-26}{"Git.Git",-44}{"2.48.1",-18}{"2.49.0",-10}winget");
        check(compactColumns.HasTable && compactColumns.RejectedRows == 0 && compactColumns.Apps.Single().CanUpdate,
            "Real WinGet header accepts a single space before Source");
        var git = parsed.Apps.Single(a => a.Id == "Git.Git");
        check(git.InstalledVersion == "2.48.1" && git.AvailableVersion == "2.49.0" && git.CanUpdate,
            "Installed and available versions come from the WinGet result");
        var local = parsed.Apps.Single(a => a.Name == "Local tool");
        check(local.IsInstalled && !local.CanAction && !local.ActionButtonVisible && local.InventoryOnly,
            "Unmatched installed registry records cannot install or update");

        var catalog = new[]
        {
            new AppPackageItem { Id = "Git.Git", Name = "Git", Source = "winget", IsPackageIdVerified = true },
            new AppPackageItem { Id = "GitHub.GitHubDesktop", Name = "GitHub Desktop", Source = "winget", IsPackageIdVerified = true, HasUpdate = true, AvailableVersion = "old" },
            new AppPackageItem { Id = "Git", Name = "Other program", Source = "winget", HasUpdate = true }
        };
        SoftwareInstallerService.ApplyInventoryToCatalog(catalog, new(parsed.Apps, true, true, "fixture"));
        check(catalog[0].HasUpdate && catalog[1].IsInstalled && !catalog[1].HasUpdate && catalog[1].AvailableVersion == "",
            "Refreshing inventory clears stale update flags and versions");
        check(!catalog[2].IsInstalled && !catalog[2].HasUpdate,
            "A substring of another package ID is never an installed-package match");
        SoftwareInstallerService.ApplyInventoryToCatalog(catalog, new(Array.Empty<AppPackageItem>(), false, false, "offline"));
        check(catalog.All(a => !a.CanAction && !a.HasUpdate && !a.UpdatesChecked && a.InstalledVersion == ""),
            "A failed source refresh removes stale update claims and disables uncertain actions");

        var truncated = SoftwareInventoryParser.Parse(heading +
            Row("Truncated", "Publisher.Long…", "1.0", "2.0", "winget") + "\n" +
            Row("Ascii truncation", "Publisher.Long...", "1.0", "2.0", "winget") + "\n" +
            Row("Unknown version", "Publisher.Unknown", "Unknown", "2.0", "winget") + "\n" +
            Row("Custom source", "Publisher.Untrusted", "1.0", "2.0", "private") + "\n");
        check(truncated.Apps.Count == 4 && truncated.Apps.All(a => !a.CanUpdate && !a.HasUpdate),
            "Truncated IDs, unknown installed versions and unverified sources cannot update");
        var polish = SoftwareInventoryParser.Parse(Row("Nazwa", "Identyfikator", "Wersja", "Dostępne", "Źródło") + "\n" +
            new string('-', 114) + "\n" + Row("Edytor zdjęć", "Photo.Editor", "1.0", "2.0", "winget"));
        check(polish.Apps.Single().CanUpdate && polish.Apps.Single().Name == "Edytor zdjęć",
            "Polish column headings preserve real package versions");
        var noAvailable = SoftwareInventoryParser.Parse($"{"Name",-26}{"Id",-44}{"Version",-18}Source\n" +
            new string('-', 95) + "\n" + $"{"Git",-26}{"Git.Git",-44}{"2.48.1",-18}winget");
        check(noAvailable.Apps.Single().InstalledVersion == "2.48.1" && !noAvailable.Apps.Single().HasUpdate,
            "A WinGet table without an Available column does not invent a newer version");
        var cjk = SoftwareInventoryParser.Parse(heading + "编辑器" + new string(' ', 20) +
            $"{"Publisher.Editor",-44}{"1.0",-18}{"2.0",-18}winget");
        check(cjk.Apps.Single().Id == "Publisher.Editor" && cjk.Apps.Single().CanUpdate,
            "Wide characters in app names cannot shift the package ID or version columns");
        check(!SoftwareInventoryParser.Parse("Failed when opening source").HasTable,
            "A WinGet failure message is not an empty healthy inventory");
        check(!SoftwareInventoryParser.IsActionableId("Publisher.App\" --all", "winget") &&
            !SoftwareInventoryParser.IsActionableId("ARP\\User\\App", "winget"),
            "Command-looking and unmatched registry identifiers are rejected as update targets");

        var service = new SoftwareInstallerService();
        check(service.GetCuratedCatalog().All(a => a.CanInstall && !a.InventoryOnly),
            "Only explicit catalogue entries can offer a fresh installation");
        check(!service.InstallAppAsync(local, null).GetAwaiter().GetResult() &&
            !service.UpgradeAppAsync(local).GetAwaiter().GetResult(),
            "Mutation entry points reject unverified records before starting any process");
    }
}
