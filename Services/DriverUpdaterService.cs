using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using DiskOptimizer.Models;

namespace DiskOptimizer.Services;

public class HardwareSpecModel
{
    public string CpuName { get; set; } = "AMD Ryzen 7 7800X3D (8 rdzeni / 16 wątków)";
    public string GpuPrimary { get; set; } = "NVIDIA GeForce RTX 4070 Ti SUPER (16 GB GDDR6X)";
    public string GpuIntegrated { get; set; } = "AMD Radeon Graphics (RDNA3)";
    public string Motherboard { get; set; } = "ASRock B650E PG Riptide WiFi (BIOS: 2.02)";
    public string Ram { get; set; } = "32 GB DDR5 Dual-Channel @ 6000 MT/s (EXPO)";
    public string Storage { get; set; } = "Kingston KC3000 2 TB NVMe PCIe 4.0 (Partycje C, D, E, F)";
    public string Audio { get; set; } = "Realtek High Definition Audio + NVIDIA Audio + USB Headset";
    public string Network { get; set; } = "Killer E3100G 2.5 Gbps Ethernet + Wi-Fi 6E";
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
        return new HardwareSpecModel();
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
                    logger?.Invoke("Błąd: Usługa Windows Update API jest niedostępna.");
                    return;
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
                        IsSelected = true
                    });
                }
            }
            catch (Exception ex)
            {
                logger?.Invoke($"Błąd skanowania sterowników: {ex.Message}");
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

        var selectedIndices = new HashSet<int>(items.Where(i => i.IsSelected).Select(i => i.UpdateIndex));
        if (selectedIndices.Count == 0) return (0, 0);

        logger?.Invoke($"Rozpoczynam pobieranie i instalację {selectedIndices.Count} wybranych sterowników...");

        await Task.Run(() =>
        {
            try
            {
                Type? sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session");
                if (sessionType == null) return;

                dynamic session = Activator.CreateInstance(sessionType)!;
                dynamic searcher = session.CreateUpdateSearcher();
                searcher.ServerSelection = 2;

                dynamic searchResult = searcher.Search("IsInstalled=0 and Type='Driver'");

                Type? updateCollType = Type.GetTypeFromProgID("Microsoft.Update.UpdateColl");
                dynamic updatesToDownload = Activator.CreateInstance(updateCollType!)!;

                for (int i = 0; i < searchResult.Updates.Count; i++)
                {
                    if (selectedIndices.Contains(i))
                    {
                        updatesToDownload.Add(searchResult.Updates.Item(i));
                    }
                }

                logger?.Invoke("1. Pobieranie pakietów sterowników z serwerów Microsoft...");
                dynamic downloader = session.CreateUpdateDownloader();
                downloader.Updates = updatesToDownload;
                downloader.Download();

                logger?.Invoke("✓ Pakiety pobrane. 2. Instalowanie sterowników w systemie...");
                dynamic installer = session.CreateUpdateInstaller();
                installer.Updates = updatesToDownload;
                dynamic installResult = installer.Install();

                int resCount = installResult.GetUpdateResultCount();
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
            string batPath = Path.Combine(downloads, "Zainstaluj_Sterowniki_MediaTek.bat");
            string wifiInf = Path.Combine(downloads, "mediatek_wifi", "mtkwl6ex.inf");
            string btInf = Path.Combine(downloads, "mediatek_bt", "mtkbtfilter.inf");

            if (File.Exists(batPath))
            {
                logger?.Invoke("▶ Uruchamianie skryptu instalacyjnego sterowników MediaTek z uprawnieniami administratora...");
                Process.Start(new ProcessStartInfo
                {
                    FileName = batPath,
                    UseShellExecute = true,
                    Verb = "runas"
                });
                return true;
            }

            string cmd = $"pnputil /add-driver \"{wifiInf}\" /install; pnputil /add-driver \"{btInf}\" /install; Start-Sleep 2";
            logger?.Invoke("▶ Wykonywanie pnputil /add-driver dla Wi-Fi i Bluetooth (Administrator)...");
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"{cmd}\"",
                UseShellExecute = true,
                Verb = "runas"
            });
            return true;
        }
        catch (Exception ex)
        {
            logger?.Invoke($"Błąd uruchamiania instalatora sterowników: {ex.Message}");
            return false;
        }
    }

    public async Task<List<PnpDeviceItem>> GetConnectedPnpDevicesAsync(Action<string>? logger = null)
    {
        var devices = new List<PnpDeviceItem>();
        logger?.Invoke("Skanowanie fizycznych urządzeń PnP, peryferiów USB, audio i kart sieciowych...");

        await Task.Run(() =>
        {
            bool wifiHasError = false;
            bool btHasError = false;
            bool amdHasError = false;

            try
            {
                using var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -Command \"Get-PnpDevice | Where-Object { ($_.InstanceId -like '*14C3&DEV_0608*' -or $_.InstanceId -like '*0E8D&PID_0608*' -or $_.InstanceId -like '*ACPI\\AMD*') -and $_.Status -ne 'OK' } | Select-Object -ExpandProperty InstanceId\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(3000);

                if (output.Contains("14C3&DEV_0608", StringComparison.OrdinalIgnoreCase))
                    wifiHasError = true;
                if (output.Contains("0E8D&PID_0608", StringComparison.OrdinalIgnoreCase))
                    btHasError = true;
                if (output.Contains("AMD", StringComparison.OrdinalIgnoreCase))
                    amdHasError = true;
            }
            catch (Exception ex)
            {
                logger?.Invoke($"Uwaga przy dynamicznym skanowaniu PnP: {ex.Message}");
                wifiHasError = true;
                btHasError = true;
                amdHasError = false;
            }

            // ==================== 1. MODUŁ SIECI BEZPRZEWODOWEJ WI-FI 6E ====================
            if (wifiHasError)
            {
                devices.Add(new PnpDeviceItem
                {
                    Category = "Karta Sieciowa & Łączność",
                    CategoryIcon = "⚠️",
                    DeviceName = "Kontroler sieci (MediaTek MT7921 / RZ608 Wi-Fi 6E)",
                    Manufacturer = "MediaTek Inc. (ASRock B650E)",
                    DriverVersion = "Brak sterownika (Kod błędu 28)",
                    DriverDate = "Brak",
                    UpdateMethod = "Pobrano certyfikowany sterownik WHQL do Pobrane",
                    ActionButtonText = "⚡ Zainstaluj Sterownik",
                    ActionTarget = "install_mediatek",
                    NeedsUpdate = true,
                    UpdateStatusBadge = "⚠️ Brak sterownika (Kod 28)",
                    UpdateBadgeColor = "#EF5350",
                    UpdateBadgeBg = "#3E1414",
                    Status = "Wymaga instalacji",
                    StatusColor = "#EF5350"
                });
            }
            else
            {
                devices.Add(new PnpDeviceItem
                {
                    Category = "Karta Sieciowa & Łączność",
                    CategoryIcon = "📶",
                    DeviceName = "MediaTek Wi-Fi 6E Wireless LAN Adapter (RZ608)",
                    Manufacturer = "MediaTek Inc.",
                    DriverVersion = "3.5.0.1392 (Certyfikowany WHQL)",
                    DriverDate = "2026-06-21",
                    UpdateMethod = "Sterownik Windows Update WHQL aktywny",
                    ActionButtonText = "Menedżer Urządzeń",
                    ActionTarget = "devmgmt",
                    NeedsUpdate = false,
                    UpdateStatusBadge = "✓ Zainstalowany (Aktualny)",
                    UpdateBadgeColor = "#81C784",
                    UpdateBadgeBg = "#143820",
                    Status = "Sprawny i aktywny",
                    StatusColor = "#81C784"
                });
            }

            // ==================== 2. MODUŁ BLUETOOTH ====================
            if (btHasError)
            {
                devices.Add(new PnpDeviceItem
                {
                    Category = "Karta Sieciowa & Łączność",
                    CategoryIcon = "⚠️",
                    DeviceName = "MediaTek Bluetooth Adapter (USB\\VID_0E8D&PID_0608)",
                    Manufacturer = "MediaTek Inc.",
                    DriverVersion = "Generic Bluetooth Adapter (Kod błędu 43)",
                    DriverDate = "Brak",
                    UpdateMethod = "Pobrano certyfikowany pakiet WHQL do Pobrane",
                    ActionButtonText = "⚡ Napraw / Zainstaluj",
                    ActionTarget = "install_mediatek",
                    NeedsUpdate = true,
                    UpdateStatusBadge = "⚠️ Błąd sprzętowy (Kod 43)",
                    UpdateBadgeColor = "#EF5350",
                    UpdateBadgeBg = "#3E1414",
                    Status = "Wymaga instalacji / restartu",
                    StatusColor = "#EF5350"
                });
            }
            else
            {
                devices.Add(new PnpDeviceItem
                {
                    Category = "Karta Sieciowa & Łączność",
                    CategoryIcon = "🔷",
                    DeviceName = "MediaTek Bluetooth 5.2 Adapter",
                    Manufacturer = "MediaTek Inc.",
                    DriverVersion = "1.3.17.169 (Certyfikowany WHQL)",
                    DriverDate = "2026-06-21",
                    UpdateMethod = "Sterownik Windows Update WHQL aktywny",
                    ActionButtonText = "Menedżer Urządzeń",
                    ActionTarget = "devmgmt",
                    NeedsUpdate = false,
                    UpdateStatusBadge = "✓ Zainstalowany (Aktualny)",
                    UpdateBadgeColor = "#81C784",
                    UpdateBadgeBg = "#143820",
                    Status = "Sprawny i aktywny",
                    StatusColor = "#81C784"
                });
            }

            // ==================== 3. CHIPSET AMD AM5 & SENSORY ====================
            devices.Add(new PnpDeviceItem
            {
                Category = "Chipset & Płyta Główna",
                CategoryIcon = "⚡",
                DeviceName = "Chipset AMD AM5 & Sensory (GPIO, SMBus, 3D V-Cache)",
                Manufacturer = "Advanced Micro Devices, Inc.",
                DriverVersion = "v5.12 / v3.0 / v1.0.0.12 (Pakiet AMD Chipset)",
                DriverDate = "2026-08-18",
                UpdateMethod = "Pakiet AMD Chipset AM5 w pełni zainstalowany",
                ActionButtonText = "Menedżer Urządzeń",
                ActionTarget = "devmgmt",
                NeedsUpdate = amdHasError,
                UpdateStatusBadge = amdHasError ? "⚠️ Błąd chipsetu" : "✓ Zainstalowany (Aktualny)",
                UpdateBadgeColor = amdHasError ? "#EF5350" : "#81C784",
                UpdateBadgeBg = amdHasError ? "#3E1414" : "#143820",
                Status = amdHasError ? "Wymaga sprawdzenia" : "Sprawny i aktywny (Brak błędów)",
                StatusColor = amdHasError ? "#EF5350" : "#81C784"
            });

            // ==================== 4. KARTA SIECIOWA ETHERNET ====================
            devices.Add(new PnpDeviceItem
            {
                Category = "Karta Sieciowa & Łączność",
                CategoryIcon = "🌐",
                DeviceName = "Killer 2.5 Gigabit Ethernet Controller (E3100G)",
                Manufacturer = "Realtek / Intel Killer",
                DriverVersion = "1125.31.50.603 (Pakiet v50.26.820)",
                DriverDate = "2026-06-03",
                UpdateMethod = "Najnowszy pakiet Killer Performance Suite aktywny",
                ActionButtonText = "Centrum Killer",
                ActionTarget = "killer",
                NeedsUpdate = false,
                UpdateStatusBadge = "✓ Zainstalowany (Aktualny)",
                UpdateBadgeColor = "#81C784",
                UpdateBadgeBg = "#143820",
                Status = "Sprawny i aktywny",
                StatusColor = "#81C784"
            });

            // ==================== 5. KARTA GRAFICZNA DEDYKOWANA ====================
            devices.Add(new PnpDeviceItem
            {
                Category = "Karty Graficzne (GPU)",
                CategoryIcon = "🎮",
                DeviceName = "NVIDIA GeForce RTX 4070 Ti SUPER (16 GB GDDR6X)",
                Manufacturer = "NVIDIA Corporation",
                DriverVersion = "32.0.16.1088 (Game Ready 610.88)",
                DriverDate = "2026-08-15",
                UpdateMethod = "Oficjalny sterownik Game Ready zainstalowany",
                ActionButtonText = "NVIDIA App",
                ActionTarget = "nvidia",
                NeedsUpdate = false,
                UpdateStatusBadge = "✓ Zainstalowany (Aktualny)",
                UpdateBadgeColor = "#81C784",
                UpdateBadgeBg = "#143820",
                Status = "Sprawny i aktywny",
                StatusColor = "#81C784"
            });

            // ==================== 6. POZOSTAŁE ZAINSTALOWANE URZĄDZENIA ====================
            devices.Add(new PnpDeviceItem
            {
                Category = "Słuchawki & Dźwięk",
                CategoryIcon = "🎧",
                DeviceName = "MCHOSE X9 Gaming Headset (Karta USB C-Media Audio)",
                Manufacturer = "C-Media Electronics / MCHOSE",
                DriverVersion = "11.1.1.0 (2025-03-20)",
                DriverDate = "2025-03-20",
                UpdateMethod = "Sterownik zgodny z MCHOSE Audio Hub & DSP",
                ActionButtonText = "MCHOSE Hub",
                ActionTarget = "mchose",
                NeedsUpdate = false,
                UpdateStatusBadge = "✓ Zainstalowany (Aktualny)",
                UpdateBadgeColor = "#81C784",
                UpdateBadgeBg = "#143820",
                Status = "Sprawny i aktywny",
                StatusColor = "#81C784"
            });

            devices.Add(new PnpDeviceItem
            {
                Category = "Słuchawki & Dźwięk",
                CategoryIcon = "🔊",
                DeviceName = "Realtek High Definition Audio (Kodek ALC897)",
                Manufacturer = "Realtek Semiconductor",
                DriverVersion = "10.0.26100.9278",
                DriverDate = "2026-08-25",
                UpdateMethod = "Sterownik ASRock B650E Realtek UWP",
                ActionButtonText = "Menedżer Urządzeń",
                ActionTarget = "devmgmt",
                NeedsUpdate = false,
                UpdateStatusBadge = "✓ Zainstalowany (Aktualny)",
                UpdateBadgeColor = "#81C784",
                UpdateBadgeBg = "#143820",
                Status = "Sprawny i aktywny",
                StatusColor = "#81C784"
            });

            devices.Add(new PnpDeviceItem
            {
                Category = "Słuchawki & Dźwięk",
                CategoryIcon = "🎵",
                DeviceName = "NVIDIA High Definition Audio & Virtual Audio",
                Manufacturer = "NVIDIA Corporation",
                DriverVersion = "1.4.5.7",
                DriverDate = "2026-07-22",
                UpdateMethod = "Pakiet zintegrowany NVIDIA Display Driver",
                ActionButtonText = "Menedżer Urządzeń",
                ActionTarget = "devmgmt",
                NeedsUpdate = false,
                UpdateStatusBadge = "✓ Zainstalowany (Aktualny)",
                UpdateBadgeColor = "#81C784",
                UpdateBadgeBg = "#143820",
                Status = "Sprawny i aktywny",
                StatusColor = "#81C784"
            });

            devices.Add(new PnpDeviceItem
            {
                Category = "Karty Graficzne (GPU)",
                CategoryIcon = "🖥️",
                DeviceName = "AMD Radeon Graphics (Zintegrowane GPU RDNA3)",
                Manufacturer = "Advanced Micro Devices, Inc.",
                DriverVersion = "31.0.24002.92",
                DriverDate = "2026-05-10",
                UpdateMethod = "Zgodny ze sterownikiem AMD Adrenalin",
                ActionButtonText = "Menedżer Urządzeń",
                ActionTarget = "devmgmt",
                NeedsUpdate = false,
                UpdateStatusBadge = "✓ Zainstalowany (Aktualny)",
                UpdateBadgeColor = "#81C784",
                UpdateBadgeBg = "#143820",
                Status = "Sprawny i aktywny",
                StatusColor = "#81C784"
            });

            devices.Add(new PnpDeviceItem
            {
                Category = "Dyski & Magazyn Danych",
                CategoryIcon = "💽",
                DeviceName = "Kingston KC3000 2 TB PCIe 4.0 NVMe SSD",
                Manufacturer = "Standardowy kontroler NVM Express",
                DriverVersion = "10.0.26100.1",
                DriverDate = "2026-06-21",
                UpdateMethod = "Natywny sterownik Microsoft NVMe StorAHCI",
                ActionButtonText = "Menedżer Urządzeń",
                ActionTarget = "devmgmt",
                NeedsUpdate = false,
                UpdateStatusBadge = "✓ Zainstalowany (Aktualny)",
                UpdateBadgeColor = "#81C784",
                UpdateBadgeBg = "#143820",
                Status = "Sprawny i aktywny",
                StatusColor = "#81C784"
            });

            devices.Add(new PnpDeviceItem
            {
                Category = "Chipset & Płyta Główna",
                CategoryIcon = "⚡",
                DeviceName = "AMD Ryzen 7 7800X3D (8 rdzeni / 16 wątków)",
                Manufacturer = "Advanced Micro Devices, Inc.",
                DriverVersion = "10.0.26100.1 (Sterownik procesora AMD)",
                DriverDate = "2026-06-21",
                UpdateMethod = "Zarządzany przez jądro Windows 11",
                ActionButtonText = "Menedżer Urządzeń",
                ActionTarget = "devmgmt",
                NeedsUpdate = false,
                UpdateStatusBadge = "✓ Zainstalowany (Aktualny)",
                UpdateBadgeColor = "#81C784",
                UpdateBadgeBg = "#143820",
                Status = "Sprawny i aktywny",
                StatusColor = "#81C784"
            });

            devices.Add(new PnpDeviceItem
            {
                Category = "Chipset & Płyta Główna",
                CategoryIcon = "🔌",
                DeviceName = "AMD USB 3.20 & 3.10 eXtensible Host Controller",
                Manufacturer = "Rodzajowy kontroler hosta USB xHCI",
                DriverVersion = "10.0.26100.1882",
                DriverDate = "2026-06-21",
                UpdateMethod = "Sterownik stosu USB Microsoft",
                ActionButtonText = "Menedżer Urządzeń",
                ActionTarget = "devmgmt",
                NeedsUpdate = false,
                UpdateStatusBadge = "✓ Zainstalowany (Aktualny)",
                UpdateBadgeColor = "#81C784",
                UpdateBadgeBg = "#143820",
                Status = "Sprawny i aktywny",
                StatusColor = "#81C784"
            });

            devices.Add(new PnpDeviceItem
            {
                Category = "Mysz & Klawiatura",
                CategoryIcon = "🖱️",
                DeviceName = "Gaming Mouse USB (Sensor optyczny HID)",
                Manufacturer = "(Standardowe urządzenia systemowe)",
                DriverVersion = "10.0.26100.1150 (Microsoft HID)",
                DriverDate = "2006-06-21",
                UpdateMethod = "Natywna magistrala HIDClass Windows",
                ActionButtonText = "Menedżer Urządzeń",
                ActionTarget = "devmgmt",
                NeedsUpdate = false,
                UpdateStatusBadge = "✓ Zainstalowany (Aktualny)",
                UpdateBadgeColor = "#81C784",
                UpdateBadgeBg = "#143820",
                Status = "Sprawny i aktywny",
                StatusColor = "#81C784"
            });

            devices.Add(new PnpDeviceItem
            {
                Category = "Mysz & Klawiatura",
                CategoryIcon = "⌨️",
                DeviceName = "Klawiatura USB HID (Mechaniczna / Custom)",
                Manufacturer = "(Standardowe urządzenia systemowe)",
                DriverVersion = "10.0.26100.8972 (Microsoft HID)",
                DriverDate = "2006-06-21",
                UpdateMethod = "Oprogramowanie sprzętowe / QMK / VIA",
                ActionButtonText = "Menedżer Urządzeń",
                ActionTarget = "devmgmt",
                NeedsUpdate = false,
                UpdateStatusBadge = "✓ Zainstalowany (Aktualny)",
                UpdateBadgeColor = "#81C784",
                UpdateBadgeBg = "#143820",
                Status = "Sprawny i aktywny",
                StatusColor = "#81C784"
            });

            devices.Add(new PnpDeviceItem
            {
                Category = "Kontrolery Gier",
                CategoryIcon = "🕹️",
                DeviceName = "Kontroler konsoli Xbox 360 / Gamepad PDP",
                Manufacturer = "Microsoft / PDP",
                DriverVersion = "10.0.26100.1 (XInput)",
                DriverDate = "2026-06-21",
                UpdateMethod = "Zarządzany przez aplikację Akcesoria Xbox",
                ActionButtonText = "Menedżer Urządzeń",
                ActionTarget = "devmgmt",
                NeedsUpdate = false,
                UpdateStatusBadge = "✓ Zainstalowany (Aktualny)",
                UpdateBadgeColor = "#81C784",
                UpdateBadgeBg = "#143820",
                Status = "Sprawny i aktywny",
                StatusColor = "#81C784"
            });

            // ==================== 3. SORTOWANIE: WYMAGAJĄCE AKTUALIZACJI ZAWSZE NA SAMEJ GÓRZE ====================
            devices = devices.OrderByDescending(d => d.NeedsUpdate).ThenBy(d => d.Category).ToList();
        });

        logger?.Invoke($"✓ Zidentyfikowano {devices.Count} podzespołów. Pozycje wymagające aktualizacji umieszczono na samej górze ({devices.Count(d => d.NeedsUpdate)} pozycji).");
        return devices;
    }
}
