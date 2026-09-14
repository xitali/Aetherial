using System.IO;
using DiskOptimizer.Models;

namespace DiskOptimizer.Services;

public class DiskScannerService
{
    public List<DriveModel> GetDrives()
    {
        var result = new List<DriveModel>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady || drive.DriveType != DriveType.Fixed) continue;

                result.Add(new DriveModel
                {
                    DriveLetter = drive.Name.TrimEnd('\\', ':'),
                    VolumeLabel = drive.VolumeLabel,
                    TotalBytes = drive.TotalSize,
                    FreeBytes = drive.AvailableFreeSpace
                });
            }
        }
        catch { }
        return result;
    }

    public List<CleanItem> GetDefaultItems()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string tempPath = Path.GetTempPath();
        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string winTemp = Path.Combine(winDir, "Temp");
        string videosPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

        var allCandidates = new List<CleanItem>
        {
            // ==================== 1. SZYBKIE CZYSZCZENIE & SYSTEM ====================
            new CleanItem
            {
                Id = "recycle_bin",
                Title = "Kosz systemowy (Recycle Bin)",
                Description = "Tymczasowo usunięte pliki ze wszystkich partycji czekające w koszu Windows.",
                Category = "Szybkie czyszczenie",
                Icon = "🗑️",
                ActionType = CleanActionType.RecycleBin,
                BadgeText = "Bezpieczne",
                BadgeColor = "#2E7D32"
            },
            new CleanItem
            {
                Id = "crash_dumps",
                Title = "Zrzuty awarii programów (CrashDumps)",
                Description = "Zrzuty pamięci (.dmp) generowane przez awarie procesów użytkownika (np. python.exe).",
                Category = "Szybkie czyszczenie",
                Icon = "💥",
                Path = Path.Combine(localAppData, "CrashDumps"),
                ActionType = CleanActionType.DeleteFiles,
                BadgeText = "100% Zbędne",
                BadgeColor = "#1B5E20"
            },
            new CleanItem
            {
                Id = "wer_reports",
                Title = "Raporty błędów Windows Error Reporting (WER)",
                Description = "Kolejki i archiwa raportów o błędach w ProgramData i AppData.",
                Category = "Szybkie czyszczenie",
                Icon = "📋",
                Path = Path.Combine(programData, @"Microsoft\Windows\WER"),
                ActionType = CleanActionType.DeleteFiles,
                BadgeText = "Bezpieczne",
                BadgeColor = "#2E7D32"
            },
            new CleanItem
            {
                Id = "user_temp",
                Title = "Pliki tymczasowe użytkownika (%TEMP%)",
                Description = "Pozostałości po instalatorach i zamkniętych aplikacjach w folderze Temp.",
                Category = "Szybkie czyszczenie",
                Icon = "⏳",
                Path = tempPath,
                ActionType = CleanActionType.DeleteFiles,
                BadgeText = "Starsze niż 1 dzień",
                BadgeColor = "#0288D1"
            },
            new CleanItem
            {
                Id = "windows_temp",
                Title = "Pliki tymczasowe Windows",
                Description = "Tymczasowe pliki generowane przez usługi systemowe Windows.",
                Category = "Szybkie czyszczenie",
                Icon = "🗂️",
                Path = winTemp,
                ActionType = CleanActionType.DeleteFiles,
                BadgeText = "Wymaga Admina",
                BadgeColor = "#E65100"
            },
            new CleanItem
            {
                Id = "windows_update_cache",
                Title = "Pobrane aktualizacje Windows Update (SoftwareDistribution)",
                Description = "Tymczasowe archiwa instalacyjne pobrane przez usługę Windows Update.",
                Category = "Szybkie czyszczenie",
                Icon = "🔄",
                Path = Path.Combine(winDir, @"SoftwareDistribution\Download"),
                ActionType = CleanActionType.DeleteFiles,
                BadgeText = "Windows Update",
                BadgeColor = "#00838F"
            },

            // ==================== 2. PRZEGLĄDARKI I KOMUNIKATORY ====================
            new CleanItem
            {
                Id = "brave_cache",
                Title = "Pamięć podręczna Brave Browser",
                Description = "Pliki cache stron internetowych w przeglądarce Brave.",
                Category = "Przeglądarki i Komunikatory",
                Icon = "🦁",
                Path = Path.Combine(localAppData, @"BraveSoftware\Brave-Browser\User Data\Default\Cache"),
                ActionType = CleanActionType.DeleteFiles,
                BadgeText = "Brave Cache",
                BadgeColor = "#D84315"
            },
            new CleanItem
            {
                Id = "chrome_cache",
                Title = "Pamięć podręczna Google Chrome",
                Description = "Pobrane pliki stron internetowych, obrazy i skrypty w Chrome.",
                Category = "Przeglądarki i Komunikatory",
                Icon = "🌐",
                Path = Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Cache"),
                ActionType = CleanActionType.DeleteFiles,
                BadgeText = "Chrome Cache",
                BadgeColor = "#1565C0"
            },
            new CleanItem
            {
                Id = "edge_cache",
                Title = "Pamięć podręczna Microsoft Edge",
                Description = "Pamięć podręczna przeglądarki Microsoft Edge.",
                Category = "Przeglądarki i Komunikatory",
                Icon = "🌊",
                Path = Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Cache"),
                ActionType = CleanActionType.DeleteFiles,
                BadgeText = "Edge Cache",
                BadgeColor = "#00695C"
            },
            new CleanItem
            {
                Id = "discord_cache",
                Title = "Pamięć podręczna Discord",
                Description = "Tymczasowe pliki multimedialne i cache komunikatora Discord.",
                Category = "Przeglądarki i Komunikatory",
                Icon = "💬",
                Path = Path.Combine(appData, @"discord\Cache"),
                ActionType = CleanActionType.DeleteFiles,
                BadgeText = "Discord",
                BadgeColor = "#4527A0"
            },

            // ==================== 3. NARZĘDZIA DEWELOPERSKIE ====================
            new CleanItem
            {
                Id = "npm_cache",
                Title = "Pamięć podręczna Node.js (npm-cache)",
                Description = "Pobrane moduły i archiwa pakietów w lokalnym cache npm.",
                Category = "Środowisko programisty",
                Icon = "📦",
                Path = Path.Combine(localAppData, "npm-cache"),
                ActionType = CleanActionType.Command,
                Command = "npm",
                CommandArgs = "cache clean --force",
                BadgeText = "npm",
                BadgeColor = "#C62828"
            },
            new CleanItem
            {
                Id = "uv_cache",
                Title = "Pamięć podręczna menedżera uv (Python)",
                Description = "Pobrane koła i archiwa pakietów Pythona w menedżerze uv.",
                Category = "Środowisko programisty",
                Icon = "⚡",
                Path = Path.Combine(localAppData, @"uv\cache"),
                ActionType = CleanActionType.Command,
                Command = "uv",
                CommandArgs = "cache clean",
                BadgeText = "uv",
                BadgeColor = "#4527A0"
            },
            new CleanItem
            {
                Id = "pip_cache",
                Title = "Pamięć podręczna pip (Python)",
                Description = "Pobrane paczki i koła instalatora pip.",
                Category = "Środowisko programisty",
                Icon = "🐍",
                Path = Path.Combine(localAppData, @"pip\cache"),
                ActionType = CleanActionType.Command,
                Command = "pip",
                CommandArgs = "cache purge",
                BadgeText = "pip",
                BadgeColor = "#1565C0"
            },
            new CleanItem
            {
                Id = "nuget_cache",
                Title = "Pamięć podręczna pakietów .NET NuGet",
                Description = "Globalny folder pakietów C# i bibliotek .NET.",
                Category = "Środowisko programisty",
                Icon = "🔷",
                Path = Path.Combine(userProfile, @".nuget\packages"),
                ActionType = CleanActionType.Command,
                Command = "dotnet",
                CommandArgs = "nuget locals all --clear",
                BadgeText = "NuGet",
                BadgeColor = "#00838F"
            },
            new CleanItem
            {
                Id = "huggingface_cache",
                Title = "Pamięć podręczna HuggingFace Hub",
                Description = "Tokenizery, wagi modeli i pliki konfiguracyjne w ~/.cache/huggingface.",
                Category = "Środowisko programisty",
                Icon = "🤗",
                Path = Path.Combine(userProfile, @".cache\huggingface"),
                ActionType = CleanActionType.DeleteFiles,
                BadgeText = "HuggingFace",
                BadgeColor = "#EF6C00"
            },
            new CleanItem
            {
                Id = "vscode_cache",
                Title = "Pamięć podręczna edytora VS Code / Trae",
                Description = "Tymczasowa pamięć podręczna edytora kodu.",
                Category = "Środowisko programisty",
                Icon = "💻",
                Path = Path.Combine(appData, @"Code\Cache"),
                ActionType = CleanActionType.DeleteFiles,
                BadgeText = "Edytor",
                BadgeColor = "#0277BD"
            },

            // ==================== 4. GRY I KARTA GRAFICZNA ====================
            new CleanItem
            {
                Id = "nvidia_ota",
                Title = "Instalatory sterowników NVIDIA OTA",
                Description = "Stare paczki aktualizacji sterowników Game Ready w NVIDIA App.",
                Category = "Gry i Grafika",
                Icon = "🟩",
                Path = Path.Combine(programData, @"NVIDIA Corporation\NVIDIA App\UpdateFramework\ota-artifacts"),
                ActionType = CleanActionType.DeleteFiles,
                BadgeText = "Bezpieczne",
                BadgeColor = "#2E7D32"
            },
            new CleanItem
            {
                Id = "nvidia_dxcache",
                Title = "Pamięć podręczna shaderów GPU (DirectX / OpenGL)",
                Description = "Skompilowane shadery gier DirectX i OpenGL (NVIDIA DXCache, GLCache, D3D, AMD). Zostaną bezpiecznie zregenerowane podczas gry.",
                Category = "Gry i Grafika",
                Icon = "🎮",
                Path = Path.Combine(localAppData, @"NVIDIA\DXCache"),
                ActionType = CleanActionType.SpecialAction,
                BadgeText = "Shadery GPU",
                BadgeColor = "#2E7D32"
            },

            // ==================== 5. PAMIĘCI PODRĘCZNE WINDOWS ====================
            new CleanItem
            {
                Id = "thumbnail_cache",
                Title = "Pamięć podręczna miniatur Windows (Thumbnails)",
                Description = "Bazy danych miniatur eksploratora plików (thumbcache_*.db) w AppData.",
                Category = "Szybkie czyszczenie",
                Icon = "🖼️",
                Path = Path.Combine(localAppData, @"Microsoft\Windows\Explorer"),
                ActionType = CleanActionType.DeleteFiles,
                CanClean = true,
                BadgeText = "Miniatury",
                BadgeColor = "#2E7D32"
            },
            new CleanItem
            {
                Id = "delivery_optimization",
                Title = "Optymalizacja dostarczania Windows Update (Cache)",
                Description = "Pamięć podręczna usługi Delivery Optimization używana do wymiany aktualizacji w sieci.",
                Category = "Szybkie czyszczenie",
                Icon = "🚀",
                Path = Path.Combine(winDir, @"SoftwareDistribution\DeliveryOptimization"),
                ActionType = CleanActionType.DeleteFiles,
                CanClean = true,
                BadgeText = "Windows Cache",
                BadgeColor = "#00838F"
            }
        };

        var detected = new List<CleanItem>();
        foreach (var item in allCandidates)
        {
            if (item.ActionType == CleanActionType.RecycleBin)
            {
                detected.Add(item);
            }
            else if (item.Id == "nvidia_dxcache")
            {
                if (GetShaderCachePaths().Any())
                {
                    detected.Add(item);
                }
            }
            else if (item.ActionType == CleanActionType.Command)
            {
                if ((!string.IsNullOrEmpty(item.Path) && Directory.Exists(item.Path)) ||
                    (!string.IsNullOrEmpty(item.Command) && DiskHelper.IsCommandAvailable(item.Command)))
                {
                    detected.Add(item);
                }
            }
            else if (!string.IsNullOrEmpty(item.Path))
            {
                if (Directory.Exists(item.Path) || File.Exists(item.Path))
                {
                    detected.Add(item);
                }
            }
        }

        return detected;
    }

    public static List<string> GetShaderCachePaths()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string localLow = Path.Combine(userProfile, @"AppData\LocalLow");

        var candidates = new List<string>
        {
            Path.Combine(localAppData, @"NVIDIA\DXCache"),
            Path.Combine(localLow, @"NVIDIA\DXCache"),
            Path.Combine(localAppData, @"NVIDIA\GLCache"),
            Path.Combine(localAppData, @"D3DSCache"),
            Path.Combine(localAppData, @"AMD\DxCache")
        };

        return candidates.Where(Directory.Exists).ToList();
    }

    public async Task ScanItemAsync(CleanItem item, CancellationToken ct = default)
    {
        item.IsScanning = true;
        item.IsScanned = false;
        item.SizeBytes = 0;
        item.ItemCount = 0;
        item.Status = "Trwa skanowanie...";

        try
        {
            int skipped = 0;
            void ReportSkipped(string path, Exception error) => skipped++;
            await Task.Run(() =>
            {
                if (item.ActionType == CleanActionType.RecycleBin)
                {
                    var (size, count) = DiskHelper.QueryRecycleBin();
                    item.SizeBytes = size;
                    item.ItemCount = (int)Math.Min(count, int.MaxValue);
                }
                else if (item.Id == "nvidia_dxcache")
                {
                    long totalSize = 0;
                    int totalFiles = 0;
                    foreach (var p in GetShaderCachePaths())
                    {
                        var (sz, cnt) = DiskHelper.GetDirectorySize(p, ct, ReportSkipped);
                        totalSize += sz;
                        totalFiles += cnt;
                    }
                    item.SizeBytes = totalSize;
                    item.ItemCount = totalFiles;
                }
                else if (File.Exists(item.Path))
                {
                    item.SizeBytes = new FileInfo(item.Path).Length;
                    item.ItemCount = 1;
                }
                else if (!string.IsNullOrWhiteSpace(item.Path))
                {
                    var (size, count) = DiskHelper.GetDirectorySize(item.Path, ct, ReportSkipped);
                    item.SizeBytes = size;
                    item.ItemCount = count;
                }
                else
                {
                    item.SizeBytes = 0;
                    item.ItemCount = 0;
                }
            }, ct);

            ct.ThrowIfCancellationRequested();
            if (skipped > 0)
            {
                item.Status = $"Błąd: niepełny odczyt ({skipped} pominiętych miejsc)";
                return;
            }
            item.IsScanned = true;
            item.Status = item.SizeBytes > 0
                ? $"Znaleziono {item.FormattedSize} ({item.ItemCount} el.)" 
                : "Czysto (0 B)";
        }
        catch (OperationCanceledException) { item.Status = "Skan anulowany — dane niepełne"; throw; }
        catch (Exception ex)
        {
            item.Status = $"Błąd: {ex.Message}";
        }
        finally
        {
            item.IsScanning = false;
        }
    }
}
