using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using DiskOptimizer.Models;

namespace DiskOptimizer.Services;

/// <summary>
/// Read-only manufacturer checks. No packages are downloaded or installed.
/// NVIDIA's public catalogue is queried with the detected PCI device ID and Windows version;
/// the response must additionally list the exact model, WHQL status and matching OS.
/// Other vendors remain explicitly unverified, with a link to their official support tool.
/// </summary>
public sealed class DriverCatalogService
{
    private static readonly HttpClient SharedClient = new() { Timeout = TimeSpan.FromSeconds(25) };
    private readonly HttpClient _client;
    public DriverCatalogService() : this(SharedClient) { }
    public DriverCatalogService(HttpClient client) => _client = client;

    public async Task<List<DriverAssessment>> AssessAsync(IEnumerable<PnpDeviceItem> devices,
        Action<string>? logger = null, CancellationToken ct = default)
    {
        var results = new List<DriverAssessment>();
        foreach (var device in devices)
        {
            ct.ThrowIfCancellationRequested();
            var assessment = CreateUnverified(device);
            if (TryGetNvidiaDeviceId(device, out var pciId) &&
                string.Equals(device.Category, "Display", StringComparison.OrdinalIgnoreCase))
            {
                logger?.Invoke($"Katalog NVIDIA: {device.DeviceName} (PCI {pciId})…");
                if (!TryGetWindowsCatalogId(device, out var osId))
                    assessment = assessment with { Detail = "Automatyczna weryfikacja NVIDIA wymaga odczytanego Windows 10/11 x64. Architektura lub system nieobsługiwany." };
                else
                {
                    try
                    {
                        // PCI hexadecimal ID without 0x. The catalogue treats other forms differently.
                        var url = $"https://gfwsl.geforce.com/services_toolkit/services/com/nvidia/services/AjaxDriverService.php?func=DriverManualLookup&deviceID={pciId}&osID={osId}&languageCode=1033&isWHQL=1&dch=1&isCRD=0&sort1=0&numberOfResults=1";
                        var json = await _client.GetStringAsync(url, ct).ConfigureAwait(false);
                        assessment = ParseNvidiaResponse(device, json);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or FormatException or InvalidOperationException)
                    {
                        assessment = assessment with { Status = DriverAssessmentStatus.Error, Detail = "Katalog producenta jest niedostępny lub zwrócił nieprawidłową odpowiedź. Spróbuj ponownie. " + ex.Message };
                    }
                }
            }
            results.Add(assessment);
        }
        return results.OrderByDescending(r => r.HasUpdate).ThenBy(r => r.DeviceName).ToList();
    }

    public static bool TryGetNvidiaDeviceId(PnpDeviceItem device, out string pciId)
    {
        pciId = "";
        var ids = device.HardwareIds.Prepend(device.DeviceId);
        foreach (var id in ids)
        {
            var match = Regex.Match(id, @"^PCI\\VEN_10DE&DEV_([0-9A-F]{4})(?:&|\\|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success) { pciId = match.Groups[1].Value.ToUpperInvariant(); return true; }
        }
        return false;
    }

    public static bool TryGetWindowsCatalogId(PnpDeviceItem device, out int osId)
    {
        osId = 0;
        if (!string.Equals(device.OsArchitecture, "AMD64", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(device.OsArchitecture, "X64", StringComparison.OrdinalIgnoreCase)) return false;
        if (device.OsProductType != 1 || !Version.TryParse(device.OsVersion, out var os) || os.Major != 10 || os.Build < 17134) return false;
        osId = os.Build >= 22000 ? 135 : 57; // NVIDIA catalogue OS identifiers, not driver versions.
        return true;
    }

    public static string? NormalizeNvidiaVersion(string installed)
    {
        if (!Regex.IsMatch(installed, @"^\d+\.\d+\.\d+\.\d{1,4}$", RegexOptions.CultureInvariant) ||
            !Version.TryParse(installed, out var version) || version.Build < 10) return null;
        // E.g. Windows 32.0.16.1088 = NVIDIA 610.88 (last digit of third field + four-digit fourth field).
        int combined = (version.Build % 10) * 10000 + version.Revision;
        return $"{combined / 100}.{combined % 100:D2}";
    }

    public static DriverAssessment ParseNvidiaResponse(PnpDeviceItem device, string json)
    {
        var result = CreateUnverified(device) with { SourceName = "NVIDIA · Game Ready WHQL (DCH)" };
        if (!TryGetNvidiaDeviceId(device, out _) || !TryGetWindowsCatalogId(device, out _)) return result;
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (Read(root, "Success") != "1" || !root.TryGetProperty("IDS", out var entries) ||
            entries.ValueKind != JsonValueKind.Array || entries.GetArrayLength() == 0)
            return result with { Detail = "Katalog NVIDIA nie zwrócił sterownika dla identyfikatora tego urządzenia i systemu." };
        var entry = entries[0];
        if (!entry.TryGetProperty("downloadInfo", out var info) || info.ValueKind != JsonValueKind.Object)
            return result with { Status = DriverAssessmentStatus.Error, Detail = "Niepełna odpowiedź katalogu NVIDIA." };
        var model = NormalizeModel(device.DeviceName);
        bool modelMatched = info.TryGetProperty("series", out var series) && series.ValueKind == JsonValueKind.Array &&
            series.EnumerateArray().Any(s => s.TryGetProperty("products", out var products) && products.ValueKind == JsonValueKind.Array &&
                products.EnumerateArray().Any(p => NormalizeModel(Read(p, "productName")) == model));
        bool windows11 = Version.Parse(device.OsVersion).Build >= 22000;
        bool osMatched = info.TryGetProperty("OSList", out var osList) && osList.ValueKind == JsonValueKind.Array &&
            osList.EnumerateArray().Any(o => Read(o, "OSName").Equals(windows11 ? "Windows 11" : "Windows 10 64-bit", StringComparison.OrdinalIgnoreCase));
        var sourceUrl = Read(info, "DetailsURL");
        if (!modelMatched || !osMatched || Read(info, "Is64Bit") != "1" || Read(info, "IsWHQL") != "1" || Read(info, "IsBeta") != "0" || Read(info, "IsCRD") != "0" || Read(info, "IsDC") != "1" || !IsOfficialNvidiaUrl(sourceUrl))
            return result with { Detail = "Nie potwierdzono zgodności modelu, systemu, certyfikacji lub źródła pakietu. Sprawdź sterownik u producenta." };
        var latest = Read(info, "Version");
        var installed = NormalizeNvidiaVersion(device.DriverVersion);
        if (!Version.TryParse(latest, out var latestVersion) || latestVersion.Build >= 0)
            return result with { Status = DriverAssessmentStatus.Error, Detail = "Katalog NVIDIA zwrócił nieprawidłowy numer wersji." };
        result = result with { LatestVersion = latest, SourceUrl = sourceUrl, ReleaseDate = Read(info, "ReleaseDateTime") };
        if (installed == null || !Version.TryParse(installed, out var installedVersion))
            return result with { Status = DriverAssessmentStatus.Unknown, Detail = "Znaleziono zgodny pakiet, ale nie można porównać odczytanej wersji sterownika Windows." };
        int comparison = installedVersion.CompareTo(latestVersion);
        return result with
        {
            InstalledVersion = $"{installed} (Windows {device.DriverVersion})",
            Status = comparison < 0 ? DriverAssessmentStatus.UpdateAvailable : comparison == 0 ? DriverAssessmentStatus.UpToDate : DriverAssessmentStatus.NewerInstalled,
            Detail = "Porównano PCI ID, dokładny model i Windows x64 w katalogu Game Ready WHQL DCH. Wersje Studio i pakiety OEM mogą się różnić. Instalator producenta ponownie sprawdza zgodność."
        };
    }

    private static string NormalizeModel(string name) => Regex.Replace(Uri.UnescapeDataString(name).Replace("NVIDIA ", "", StringComparison.OrdinalIgnoreCase).Trim(), @"\s+", " ").ToUpperInvariant();
    private static string Read(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? Uri.UnescapeDataString(value.GetString() ?? "") : "";
    public static bool IsOfficialNvidiaUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo) && (uri.Host.Equals("nvidia.com", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".nvidia.com", StringComparison.OrdinalIgnoreCase));

    private static DriverAssessment CreateUnverified(PnpDeviceItem device)
    {
        var ids = string.Join(" ", device.HardwareIds.Prepend(device.DeviceId));
        var maker = device.Manufacturer;
        string source = "Producent urządzenia / komputera", url = "";
        if (ids.Contains("VEN_10DE&", StringComparison.OrdinalIgnoreCase)) { source = "NVIDIA"; url = "https://www.nvidia.com/en-us/drivers/"; }
        else if (ids.Contains("VEN_1002&", StringComparison.OrdinalIgnoreCase) || ids.Contains("VEN_1022&", StringComparison.OrdinalIgnoreCase) || maker.Contains("Advanced Micro Devices", StringComparison.OrdinalIgnoreCase)) { source = "AMD Auto-Detect"; url = "https://www.amd.com/en/support/download/drivers.html"; }
        else if (ids.Contains("VEN_8086&", StringComparison.OrdinalIgnoreCase) || maker.Contains("Intel", StringComparison.OrdinalIgnoreCase)) { source = "Intel Driver & Support Assistant"; url = "https://www.intel.com/content/www/us/en/support/detect.html"; }
        return new DriverAssessment { DeviceId = device.DeviceId, DeviceName = device.DeviceName, InstalledVersion = device.DriverVersion, SourceName = source, SourceUrl = url,
            Status = DriverAssessmentStatus.Unsupported, Detail = "Brak zintegrowanego, wiarygodnego katalogu dla tego urządzenia. Dostępność sterownika należy sprawdzić narzędziem producenta. Brak wyniku Windows Update nie potwierdza aktualności." };
    }
}
