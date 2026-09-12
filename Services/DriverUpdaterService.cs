using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DiskOptimizer.Models;

namespace DiskOptimizer.Services;

public class HardwareSpecModel
{
    public string CpuName { get; set; } = "Nie odczytano";
    public string GpuPrimary { get; set; } = "Nie odczytano";
    public string GpuIntegrated { get; set; } = "Nie odczytano";
    public string Motherboard { get; set; } = "Nie odczytano";
    public string Ram { get; set; } = "Nie odczytano";
    public string Storage { get; set; } = "Nie odczytano";
    public string Audio { get; set; } = "Nie odczytano";
    public string Network { get; set; } = "Nie odczytano";
}

public class DriverUpdateItem : INotifyPropertyChanged
{
    private bool _isSelected = true;
    private string _status = "Dostępna do instalacji";
    private string _statusColor = "#64B5F6";

    public string Title { get; set; } = string.Empty;
    public string DriverModel { get; set; } = string.Empty;
    public string Category { get; set; } = "System";
    public string CategoryIcon { get; set; } = "⚙️";
    public string ReleaseDate { get; set; } = string.Empty;
    public string UpdateId { get; set; } = string.Empty;
    public int UpdateIndex { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public string StatusColor
    {
        get => _statusColor;
        set { _statusColor = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class DriverUpdaterService
{
    public HardwareSpecModel GetHardwareSpecs()
    {
        return GetHardwareSpecsAsync().GetAwaiter().GetResult();
    }

    public async Task<List<DriverUpdateItem>> SearchDriverUpdatesAsync(Action<string>? logger = null, CancellationToken ct = default)
    {
        var list = new List<DriverUpdateItem>();
        logger?.Invoke("Wyszukiwanie certyfikowanych aktualizacji sterowników w usłudze Windows Update WHQL...");

        await Task.Run(() =>
        {
            try
            {
                Type? sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session");
                if (sessionType == null)
                {
                    throw new InvalidOperationException("Usługa Windows Update API jest niedostępna.");
                }

                dynamic session = Activator.CreateInstance(sessionType)!;
                dynamic searcher = session.CreateUpdateSearcher();
                searcher.ServerSelection = 2; // Windows Update Catalog

                dynamic searchResult = searcher.Search("IsInstalled=0 and Type='Driver'");
                int count = searchResult.Updates.Count;
                logger?.Invoke($"Znaleziono {count} dostępnych aktualizacji sterowników WHQL.");

                for (int i = 0; i < count; i++)
                {
                    if (ct.IsCancellationRequested) break;

                    dynamic update = searchResult.Updates.Item(i);
                    string title = update.Title?.ToString() ?? "Sterownik";
                    string date = update.LastDeploymentChangeTime?.ToString() ?? "Najnowszy";
                    string driverModel = update.DriverModel?.ToString() ?? "";

                    string cat = "System / Płyta główna";
                    string icon = "⚙️";

                    if (title.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) || title.Contains("Display", StringComparison.OrdinalIgnoreCase))
                    {
                        cat = "Karta graficzna (GPU)";
                        icon = "🎮";
                    }
                    else if (title.Contains("AMD", StringComparison.OrdinalIgnoreCase) || title.Contains("Chipset", StringComparison.OrdinalIgnoreCase) || title.Contains("SMBus", StringComparison.OrdinalIgnoreCase))
                    {
                        cat = "Chipset & Procesor AMD";
                        icon = "⚡";
                    }
                    else if (title.Contains("Audio", StringComparison.OrdinalIgnoreCase) || title.Contains("Sound", StringComparison.OrdinalIgnoreCase) || title.Contains("Realtek", StringComparison.OrdinalIgnoreCase))
                    {
                        cat = "Karta dźwiękowa (Audio)";
                        icon = "🎵";
                    }
                    else if (title.Contains("Ethernet", StringComparison.OrdinalIgnoreCase) || title.Contains("LAN", StringComparison.OrdinalIgnoreCase) || title.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) || title.Contains("Killer", StringComparison.OrdinalIgnoreCase))
                    {
                        cat = "Sieć & Łączność";
                        icon = "🌐";
                    }
                    else if (title.Contains("LG", StringComparison.OrdinalIgnoreCase) || title.Contains("Monitor", StringComparison.OrdinalIgnoreCase))
                    {
                        cat = "Ekran & Peryferia";
                        icon = "🖥️";
                    }

                    list.Add(new DriverUpdateItem
                    {
                        Title = title,
                        DriverModel = driverModel,
                        Category = cat,
                        CategoryIcon = icon,
                        ReleaseDate = date,
                        UpdateIndex = i,
                        UpdateId = update.Identity.UpdateID.ToString(),
                        IsSelected = true
                    });
                }
            }
            catch (Exception ex)
            {
                logger?.Invoke($"Błąd skanowania sterowników: {ex.Message}");
                throw;
            }
        }, ct);

        return list;
    }

    public async Task<(int successCount, int failedCount)> InstallSelectedUpdatesAsync(
        IEnumerable<DriverUpdateItem> items, 
        Action<string>? logger = null, 
        CancellationToken ct = default)
    {
        int success = 0;
        int failed = 0;

        var selectedIndices = new HashSet<string>(items.Where(i => i.IsSelected && !string.IsNullOrEmpty(i.UpdateId)).Select(i => i.UpdateId), StringComparer.OrdinalIgnoreCase);
        if (selectedIndices.Count == 0) return (0, 0);

        logger?.Invoke($"Rozpoczynam pobieranie i instalację {selectedIndices.Count} wybranych sterowników...");

        await Task.Run(() =>
        {
            try
            {
                Type? sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session");
                if (sessionType == null) throw new InvalidOperationException("Windows Update niedostępny.");

                dynamic session = Activator.CreateInstance(sessionType)!;
                dynamic searcher = session.CreateUpdateSearcher();
                searcher.ServerSelection = 2;

                dynamic searchResult = searcher.Search("IsInstalled=0 and Type='Driver'");

                Type? updateCollType = Type.GetTypeFromProgID("Microsoft.Update.UpdateColl");
                dynamic updatesToDownload = Activator.CreateInstance(updateCollType!)!;

                for (int i = 0; i < searchResult.Updates.Count; i++)
                {
                    if (selectedIndices.Contains((string)searchResult.Updates.Item(i).Identity.UpdateID))
                    {
                        updatesToDownload.Add(searchResult.Updates.Item(i));
                    }
                }

                if (updatesToDownload.Count == 0) throw new InvalidOperationException("Wybrane aktualizacje nie są już dostępne; odśwież listę.");
                ct.ThrowIfCancellationRequested();
                logger?.Invoke("1. Pobieranie pakietów sterowników z serwerów Microsoft...");
                dynamic downloader = session.CreateUpdateDownloader();
                downloader.Updates = updatesToDownload;
                downloader.Download();

                logger?.Invoke("✓ Pakiety pobrane. 2. Instalowanie sterowników w systemie...");
                dynamic installer = session.CreateUpdateInstaller();
                installer.Updates = updatesToDownload;
                dynamic installResult = installer.Install();

                int resCount = updatesToDownload.Count;
                for (int i = 0; i < resCount; i++)
                {
                    dynamic res = installResult.GetUpdateResult(i);
                    int resultCode = res.ResultCode; // 2 = Succeeded
                    if (resultCode == 2)
                    {
                        success++;
                    }
                    else
                    {
                        failed++;
                    }
                }

                logger?.Invoke($"🎉 Zakończono instalację! Zainstalowano: {success}, Błędy: {failed}.");
            }
            catch (Exception ex)
            {
                failed = Math.Max(failed, selectedIndices.Count - success);
                logger?.Invoke($"❌ Błąd instalatora Windows Update: {ex.Message}");
            }
        }, ct);

        return (success, failed);
    }

    public static void OpenNvidiaApp()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        string[] possiblePaths = {
            Path.Combine(progFiles, @"NVIDIA Corporation\NVIDIA App\NVIDIA App.exe"),
            Path.Combine(progFiles, @"NVIDIA Corporation\NVIDIA GeForce Experience\NVIDIA GeForce Experience.exe"),
            Path.Combine(localAppData, @"NVIDIA Corporation\NVIDIA App\NVIDIA App.exe")
        };

        foreach (var p in possiblePaths)
        {
            if (File.Exists(p))
            {
                Process.Start(p);
                return;
            }
        }

        // Fallback: otwórz oficjalną stronę pobierania sterowników GeForce
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://www.nvidia.pl/Download/index.aspx?lang=pl",
            UseShellExecute = true
        });
    }

    public static void OpenAsrockSupport()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://pg.asrock.com/mb/AMD/B650E%20PG%20Riptide%20WiFi/index.asp#Download",
            UseShellExecute = true
        });
    }

    public static void OpenMchoseHub()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://www.mchose.store/pages/software-download",
            UseShellExecute = true
        });
    }

    public static void OpenKillerIntelSuite()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://www.intel.com/content/www/us/en/download/19779/intel-killer-performance-suite.html",
            UseShellExecute = true
        });
    }

    public static void OpenAmdChipsetDrivers()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://www.amd.com/en/support/download/drivers.html",
            UseShellExecute = true
        });
    }

    public static void OpenDeviceManager()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "devmgmt.msc",
            UseShellExecute = true
        });
    }

    public static bool InstallMediaTekDrivers(Action<string>? logger = null)
    {
        try
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string downloads = Path.Combine(userProfile, "Downloads");
            string psScript = Path.Combine(downloads, "Zainstaluj_Sterowniki_MediaTek.ps1");
            string wifiInf = Path.Combine(downloads, "mediatek_wifi", "mtkwl6ex.inf");
            string btInf = Path.Combine(downloads, "mediatek_bt", "mtkbtfilter.inf");

            if (!File.Exists(wifiInf) || !File.Exists(btInf))
            {
                logger?.Invoke("Brak lokalnych pakietów INF. Użyj Windows Update lub pobierz sterowniki dla wykrytego urządzenia od producenta.");
                return false;
            }
            EnsureMediaTekInstallerFiles(downloads, wifiInf, btInf);

            logger?.Invoke("▶ Uruchamianie procedury instalacji sterowników Wi-Fi 6E & Bluetooth (PowerShell RunAs)...");

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"Start-Process powershell.exe -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \\\"{psScript}\\\"' -Verb RunAs\"",
                UseShellExecute = true
            };
            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            logger?.Invoke($"Błąd uruchamiania instalatora sterowników: {ex.Message}");
            return false;
        }
    }

    private static void EnsureMediaTekInstallerFiles(string downloads, string wifiInf, string btInf)
    {
        try
        {
            string psScript = Path.Combine(downloads, "Zainstaluj_Sterowniki_MediaTek.ps1");
            string batPath = Path.Combine(downloads, "Zainstaluj_Sterowniki_MediaTek.bat");

            string psCode = @"# Auto-elevate if not admin
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Start-Process powershell.exe -ArgumentList ""-NoProfile -ExecutionPolicy Bypass -File `""$PSCommandPath`"""" -Verb RunAs
    exit
}

$Host.UI.RawUI.WindowTitle = ""Instalacja Sterownikow MediaTek Wi-Fi 6E & Bluetooth""
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host ""====================================================================="" -ForegroundColor Cyan
Write-Host ""   INSTALACJA STEROWNIKOW MEDIATEK WI-FI 6E & BLUETOOTH (WHQL)       "" -ForegroundColor Cyan
Write-Host ""====================================================================="" -ForegroundColor Cyan
Write-Host """"

$downloads = ""$env:USERPROFILE\Downloads""
$wifiInf = Join-Path $downloads ""mediatek_wifi\mtkwl6ex.inf""
$btInf   = Join-Path $downloads ""mediatek_bt\mtkbtfilter.inf""

if (Test-Path $wifiInf) {
    Write-Host ""[1/2] Instalowanie AMD RZ608 / MediaTek MT7921 Wi-Fi 6E..."" -ForegroundColor Yellow
    Write-Host ""Plik INF: $wifiInf"" -ForegroundColor Gray
    $resWifi = & pnputil.exe /add-driver ""$wifiInf"" /install
    $resWifi | ForEach-Object { Write-Host ""  $_"" -ForegroundColor Green }
} else {
    Write-Host ""BLAD: Nie znaleziono pliku $wifiInf"" -ForegroundColor Red
}

Write-Host """"
if (Test-Path $btInf) {
    Write-Host ""[2/2] Instalowanie MediaTek Bluetooth 5.2..."" -ForegroundColor Yellow
    Write-Host ""Plik INF: $btInf"" -ForegroundColor Gray
    $resBt = & pnputil.exe /add-driver ""$btInf"" /install
    $resBt | ForEach-Object { Write-Host ""  $_"" -ForegroundColor Green }
} else {
    Write-Host ""BLAD: Nie znaleziono pliku $btInf"" -ForegroundColor Red
}

Write-Host """"
Write-Host ""Restartowanie uslugi Bluetooth..."" -ForegroundColor Yellow
Restart-Service bthserv -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

Write-Host """"
Write-Host ""====================================================================="" -ForegroundColor Cyan
Write-Host ""   STATUS MAGISTRALI PNP PO INSTALACJI:                              "" -ForegroundColor Cyan
Write-Host ""====================================================================="" -ForegroundColor Cyan
Start-Sleep -Seconds 2

Get-PnpDevice | Where-Object { 
    $_.InstanceId -like '*14C3&DEV_0608*' -or 
    $_.InstanceId -like '*0E8D&PID_0608*' -or 
    $_.FriendlyName -like '*RZ608*' -or 
    $_.FriendlyName -like '*MediaTek*' -or 
    $_.FriendlyName -like '*Generic Bluetooth*' 
} | Format-Table InstanceId, FriendlyName, Status, ConfigManagerErrorCode -AutoSize

Write-Host """"
Write-Host ""Gotowe! Wcisnij dowolny klawisz, aby zamknac okno..."" -ForegroundColor Green
try {
    $null = $Host.UI.RawUI.ReadKey(""NoEcho,IncludeKeyDown"")
} catch {
    pause
}
";
            File.WriteAllText(psScript, psCode, System.Text.Encoding.UTF8);

            string batCode = "@echo off\r\nsetlocal\r\ncd /d \"%~dp0\"\r\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File \"%~dp0Zainstaluj_Sterowniki_MediaTek.ps1\"\r\n";
            File.WriteAllText(batPath, batCode, System.Text.Encoding.ASCII);
        }
        catch { }
    }

    public async Task<HardwareSpecModel> GetHardwareSpecsAsync(Action<string>? logger = null)
    {
        const string script = """
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$cpu = @(Get-CimInstance Win32_Processor)
$gpu = @(Get-CimInstance Win32_VideoController)
$board = Get-CimInstance Win32_BaseBoard | Select-Object -First 1
$bios = Get-CimInstance Win32_BIOS | Select-Object -First 1
$ram = @(Get-CimInstance Win32_PhysicalMemory)
[pscustomobject]@{
 CpuName = ($cpu.Name -join ', ')
 GpuPrimary = ($gpu.Name -join ', ')
 GpuIntegrated = 'Klasyfikacja GPU niedostępna'
 Motherboard = "$($board.Manufacturer) $($board.Product) · BIOS $($bios.SMBIOSBIOSVersion)"
 Ram = ('{0:N1} GB · {1} modułów · {2} MT/s' -f (($ram | Measure-Object Capacity -Sum).Sum / 1GB), $ram.Count, (($ram.ConfiguredClockSpeed | Sort-Object -Unique) -join '/'))
 Storage = ((Get-CimInstance Win32_DiskDrive).Model -join ', ')
 Audio = ((Get-CimInstance Win32_SoundDevice).Name -join ', ')
 Network = ((Get-CimInstance Win32_NetworkAdapter | Where-Object PhysicalAdapter).Name -join ', ')
} | ConvertTo-Json -Compress
""";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try
        {
            var (ok, output, _) = await DiskHelper.RunPowerShellScriptAsync(script, null, timeout.Token).ConfigureAwait(false);
            if (!ok) throw new IOException(output);
            return JsonSerializer.Deserialize<HardwareSpecModel>(output) ?? new HardwareSpecModel();
        }
        catch (Exception ex)
        {
            logger?.Invoke($"Nie odczytano specyfikacji: {ex.Message}");
            return new HardwareSpecModel();
        }
    }

    public async Task<List<PnpDeviceItem>> GetConnectedPnpDevicesAsync(Action<string>? logger = null)
    {
        const string script = """
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$drivers = @{}
Get-CimInstance Win32_PnPSignedDriver | ForEach-Object { if ($_.DeviceID) { $drivers[$_.DeviceID] = $_ } }
$devices = @(Get-CimInstance Win32_PnPEntity | Where-Object { $_.Present -eq $true } | ForEach-Object {
 $driver = $drivers[$_.PNPDeviceID]
 [pscustomobject]@{ Name = $_.Name; Id = $_.PNPDeviceID; Category = $_.PNPClass; Manufacturer = $_.Manufacturer; Code = $_.ConfigManagerErrorCode; Version = $driver.DriverVersion; Date = if ($driver.DriverDate) { $driver.DriverDate.ToString('yyyy-MM-dd') } else { '' } }
})
ConvertTo-Json -InputObject $devices -Compress -Depth 3
""";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var (ok, output, _) = await DiskHelper.RunPowerShellScriptAsync(script, null, timeout.Token);
        if (!ok) throw new IOException($"Nie odczytano urządzeń PnP: {output}");
        using var doc = JsonDocument.Parse(output);
        var devices = new List<PnpDeviceItem>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            string Read(string key) => el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
            int? code = el.TryGetProperty("Code", out var c) && c.TryGetInt32(out var value) ? value : null;
            bool problem = code.HasValue && code.Value != 0;
            string color = problem ? "#F87171" : code == 0 ? "#34D399" : "#94A3B8";
            devices.Add(new PnpDeviceItem
            {
                DeviceName = string.IsNullOrWhiteSpace(Read("Name")) ? Read("Id") : Read("Name"),
                Category = Read("Category"), Manufacturer = Read("Manufacturer"), DriverVersion = string.IsNullOrEmpty(Read("Version")) ? "Brak danych" : Read("Version"),
                DriverDate = Read("Date"), NeedsUpdate = problem,
                Status = code == 0 ? "System nie zgłasza błędu" : code.HasValue ? $"Kod urządzenia: {code}" : "Stan nieznany",
                StatusColor = color, UpdateStatusBadge = problem ? "Wymaga sprawdzenia" : "Aktualność niezweryfikowana",
                UpdateBadgeColor = color, UpdateBadgeBg = problem ? "#EF444420" : "#64748B20",
                ActionButtonText = "Menedżer urządzeń", ActionTarget = "devmgmt",
                UpdateMethod = "Aktualizacje sprawdza Windows Update"
            });
        }
        logger?.Invoke($"Odczytano {devices.Count} obecnych urządzeń z Windows CIM.");
        return devices.OrderByDescending(d => d.NeedsUpdate).ThenBy(d => d.Category).ThenBy(d => d.DeviceName).ToList();
    }

    public async Task<List<DiagnosticItem>> GetDynamicDiagnosticItemsAsync(Action<string>? logger = null)
    {
        try
        {
            var devices = await GetConnectedPnpDevicesAsync(logger);
            var issues = devices.Where(d => d.NeedsUpdate).Select((d, i) => new DiagnosticItem
            {
                Id = $"pnp_{i}", Category = d.Category, Title = d.DeviceName, BadgeText = d.Status,
                Description = "Windows zgłasza problem urządzenia. Kod nie oznacza automatycznie dostępności nowego sterownika.",
                ActionButtonText = "Menedżer urządzeń", ActionTarget = "devmgmt", IsProblem = true
            }).ToList();
            issues.Add(new DiagnosticItem
            {
                Id = "pnp_summary", Category = "System", Title = "Diagnostyka urządzeń Windows",
                BadgeText = devices.Count == 0 ? "Brak danych" : $"{devices.Count} urządzeń · {issues.Count} problemów",
                Description = "Wynik dotyczy kodów urządzeń PnP. Nie potwierdza stanu SMART dysków, aktualności BIOS, profilu EXPO ani temperatur.",
                IsProblem = false, BadgeForeground = "#94A3B8", BadgeBackground = "#64748B20", BorderBrush = "#64748B"
            });
            return issues;
        }
        catch (Exception ex)
        {
            logger?.Invoke(ex.Message);
            return new List<DiagnosticItem> { new() { Id = "unavailable", Title = "Diagnostyka niedostępna", BadgeText = "Nie odczytano", Description = ex.Message, IsProblem = false } };
        }
    }
}
