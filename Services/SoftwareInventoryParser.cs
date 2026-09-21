using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DiskOptimizer.Models;

namespace DiskOptimizer.Services;

public sealed record SoftwareInventoryResult(
    IReadOnlyList<AppPackageItem> Apps, bool IsComplete, bool UpdatesChecked, string Status);

public sealed record SoftwareTableResult(IReadOnlyList<AppPackageItem> Apps, bool HasTable, int RejectedRows);

/// <summary>
/// Reads the columns emitted by WinGet, rather than searching for IDs as substrings.
/// Unknown layouts and truncated IDs never become update targets.
/// </summary>
public static class SoftwareInventoryParser
{
    private static readonly Regex Ansi = new(@"\x1B\[[0-?]*[ -/]*[@-~]", RegexOptions.Compiled);
    private static readonly Regex PackageId = new(@"^[A-Za-z0-9][A-Za-z0-9._+\-]*$", RegexOptions.Compiled);

    public static bool IsActionableId(string id, string source) =>
        !string.IsNullOrWhiteSpace(id) && !id.Contains("...") && PackageId.IsMatch(id) &&
        ((source.Equals("winget", StringComparison.OrdinalIgnoreCase) && id.Contains('.')) ||
         (source.Equals("msstore", StringComparison.OrdinalIgnoreCase) &&
          Regex.IsMatch(id, "^[A-Za-z0-9]{12}$")));

    public static SoftwareTableResult Parse(string output)
    {
        var lines = Ansi.Replace(output ?? string.Empty, string.Empty).Replace("\r", "").Split('\n');
        var apps = new List<AppPackageItem>();
        bool hasTable = false;
        int rejected = 0;
        int[]? columns = null;
        bool hasAvailable = false;
        bool hasSource = false;
        foreach (string line in lines)
        {
            // WinGet may leave just one space between Available and Source.
            // Header tokens still give exact terminal-column offsets.
            var headings = Regex.Matches(line, @"\S+");
            if (headings.Count is >= 3 and <= 5 &&
                IsHeading(headings[0].Value, "Name", "Nazwa") &&
                IsHeading(headings[1].Value, "Id", "Identyfikator") &&
                IsHeading(headings[2].Value, "Version", "Wersja"))
            {
                hasSource = IsHeading(headings[^1].Value, "Source", "Źródło");
                hasAvailable = headings.Count > 3 && IsHeading(headings[3].Value, "Available", "Dostępne", "Dostępna");
                // Only parse a known layout. Other localizations remain explicitly unverified.
                if (headings.Count != 3 + (hasSource ? 1 : 0) + (hasAvailable ? 1 : 0))
                {
                    columns = null;
                    rejected++;
                    continue;
                }
                columns = headings.Select(m => DisplayWidth(line[..m.Index])).ToArray();
                hasTable = true;
                continue;
            }
            if (columns is null || string.IsNullOrWhiteSpace(line) || Regex.IsMatch(line.Trim(), @"^-{3,}$")) continue;
            string Cell(int index) => SliceColumns(line, columns[index], index + 1 < columns.Length ? columns[index + 1] : int.MaxValue).Trim();
            // Summary/warning lines are outside the table and cannot produce package records.
            if (DisplayWidth(line.TrimEnd()) <= columns[1]) continue;
            string name = Cell(0), id = Cell(1), version = Cell(2);
            bool plausibleId = PackageId.IsMatch(id) || id.StartsWith("ARP\\", StringComparison.OrdinalIgnoreCase) || id.StartsWith("MSIX\\", StringComparison.OrdinalIgnoreCase) || id.Contains('…');
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(version) || !plausibleId)
            {
                rejected++;
                continue;
            }
            string source = hasSource ? Cell(columns.Length - 1) : string.Empty;
            string available = hasAvailable ? Cell(3) : string.Empty;
            bool verified = IsActionableId(id, source);
            bool knownVersion = IsKnownVersion(version);
            bool update = verified && knownVersion && IsKnownVersion(available) && !version.Equals(available, StringComparison.OrdinalIgnoreCase);
            apps.Add(new AppPackageItem
            {
                Id = id, Name = name, Category = "Zainstalowane", InventoryOnly = true,
                IsInstalled = true, IsPackageIdVerified = verified, Source = source,
                InstalledVersion = knownVersion ? version : "", AvailableVersion = update ? available : "",
                HasUpdate = update, UpdatesChecked = verified && knownVersion,
                Description = verified ? $"Źródło aktualizacji: {source}" : "Brak potwierdzonego pakietu w źródłach WinGet.",
                Status = update ? "Dostępna aktualizacja" : !verified ? "Aktualizacje niezweryfikowane" : !knownVersion ? "Nieznana wersja zainstalowana" : "Brak aktualizacji w WinGet",
                StatusColor = update ? "#FFB74D" : verified && knownVersion ? "#81C784" : "#888898"
            });
        }
        return new SoftwareTableResult(apps
            .DistinctBy(a => $"{a.Id}\u001f{a.InstalledVersion}\u001f{a.Name}", StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList(), hasTable, rejected);
    }

    private static bool IsHeading(string value, params string[] candidates) => candidates.Any(c => value.Equals(c, StringComparison.OrdinalIgnoreCase));
    private static bool IsKnownVersion(string value) => !string.IsNullOrWhiteSpace(value) &&
        !value.Contains('…') && !value.Contains("...") && !value.StartsWith('<') && !value.StartsWith('>') &&
        !IsHeading(value, "Unknown", "Nieznana", "Nieznany", "Nieznane");

    // WinGet pads columns by terminal cell width, which differs from UTF-16 length for CJK/emoji.
    private static int RuneWidth(Rune rune)
    {
        if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format) return 0;
        int c = rune.Value;
        return c >= 0x1100 && (c <= 0x115f || c is 0x2329 or 0x232a ||
            c is >= 0x2e80 and <= 0xa4cf || c is >= 0xac00 and <= 0xd7a3 ||
            c is >= 0xf900 and <= 0xfaff || c is >= 0xfe10 and <= 0xfe19 ||
            c is >= 0xfe30 and <= 0xfe6f || c is >= 0xff00 and <= 0xff60 ||
            c is >= 0xffe0 and <= 0xffe6 || c is >= 0x1f300 and <= 0x1faff || c >= 0x20000) ? 2 : 1;
    }

    private static int DisplayWidth(string text) => text.EnumerateRunes().Sum(RuneWidth);
    private static string SliceColumns(string text, int start, int end)
    {
        var result = new StringBuilder();
        int position = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (position >= end) break;
            if (position >= start) result.Append(rune.ToString());
            position += RuneWidth(rune);
        }
        return result.ToString();
    }
}
