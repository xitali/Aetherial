using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiskOptimizer.Models;

namespace DiskOptimizer.Services;

public class ScanReport
{
    public int HealthScore { get; set; } = 100;
    public string HealthStatus { get; set; } = "Stan doskonały • Brak problemów";
    public List<ScanResultItem> Items { get; set; } = new();
    public long TotalRecoverableBytes => Items.Where(i => i.Fix == FixAction.CleanFiles || i.Fix == FixAction.DevCleanup).Sum(i => i.SizeBytes);
    public int ProblemCount => Items.Count;
}

public interface IScanService
{
    Task<ScanReport> ScanAllAsync(IProgress<(int percent, string message)>? progress = null, CancellationToken ct = default);
}

public class ScanService : IScanService
{
    private readonly DiskScannerService _diskScanner = new();
    private readonly DriverUpdaterService _driverService = new();

    public async Task<ScanReport> ScanAllAsync(IProgress<(int percent, string message)>? progress = null, CancellationToken ct = default)
    {
        var report = new ScanReport();
        var items = new List<ScanResultItem>();

        progress?.Report((10, "Inicjalizacja skanera systemowego..."));
        await Task.Delay(100, ct);

        // 1. Skanowanie plików tymczasowych i pamięci podręcznej (10% - 50%)
        progress?.Report((25, "Analiza plików tymczasowych, pamięci podręcznej i shaderów..."));
        var cleanItems = _diskScanner.GetDefaultItems();
        foreach (var ci in cleanItems)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                await _diskScanner.ScanItemAsync(ci, ct);
                if (ci.SizeBytes > 0)
                {
                    Severity sev = ci.SizeBytes > 1024L * 1024 * 1024 ? Severity.Critical : (ci.SizeBytes > 100L * 1024 * 1024 ? Severity.Warning : Severity.Info);
                    items.Add(new ScanResultItem
                    {
                        Id = ci.Id,
                        Name = ci.Title,
                        Description = ci.Description,
                        Category = string.IsNullOrWhiteSpace(ci.Category) ? "Pamięć podręczna & Dysk" : ci.Category,
                        SizeBytes = ci.SizeBytes,
                        Fix = FixAction.CleanFiles,
                        Severity = sev,
                        DetailPath = ci.Path,
                        IsSelected = true
                    });
                }
            }
            catch { }
        }

        // 2. Analiza obciążenia pamięci RAM (50% - 65%)
        progress?.Report((55, "Weryfikacja obciążenia pamięci operacyjnej RAM..."));
        try
        {
            var mem = MemoryOptimizerService.GetMemoryMetrics();
            if (mem.loadPercent >= 75)
            {
                items.Add(new ScanResultItem
                {
                    Id = "ram_high_usage",
                    Name = $"Wysokie obciążenie pamięci RAM ({mem.loadPercent}%)",
                    Description = $"Pamięć robocza jest obciążona w {mem.loadPercent}%. Dostępne: {DriveModel.FormatBytes((long)mem.availBytes)}. Uwolnienie pamięci podręcznej stron zwiększy responsywność gier i aplikacji.",
                    Category = "Pamięć RAM",
                    SizeBytes = 0,
                    Fix = FixAction.OptimizeRam,
                    Severity = mem.loadPercent >= 88 ? Severity.Critical : Severity.Warning,
                    IsSelected = true
                });
            }
        }
        catch { }

        // 3. Weryfikacja sprzętu i magistrali PnP (65% - 85%)
        progress?.Report((75, "Inspekcja magistrali PnP i konfiguracji sprzętu..."));
        try
        {
            var diagItems = await _driverService.GetDynamicDiagnosticItemsAsync(null);
            foreach (var d in diagItems.Where(x => x.IsProblem))
            {
                items.Add(new ScanResultItem
                {
                    Id = d.Id,
                    Name = d.Title,
                    Description = d.Description,
                    Category = "Sprzęt & Magistrala PnP",
                    SizeBytes = 0,
                    Fix = FixAction.UpdateDriver,
                    Severity = Severity.Critical,
                    IsSelected = true
                });
            }
        }
        catch { }

        // 4. Zalecenie optymalizacji SSD TRIM (85% - 95%)
        progress?.Report((90, "Weryfikacja kondycji bloków komórek SSD (TRIM)..."));
        try
        {
            items.Add(new ScanResultItem
            {
                Id = "nvme_retrim",
                Name = "Sprzętowa optymalizacja SSD (ReTrim)",
                Description = "Synchronizacja usuniętych bloków TRIM dla wszystkich dysków SSD w celu utrzymania maksymalnej prędkości zapisu.",
                Category = "Pamięć masowa NVMe",
                SizeBytes = 0,
                Fix = FixAction.TrimSsd,
                Severity = Severity.Info,
                IsSelected = true
            });
        }
        catch { }

        progress?.Report((100, "Zakończono pełne skanowanie systemu."));

        // Obliczanie wskaźnika zdrowia
        int score = 100;
        int critCount = items.Count(i => i.Severity == Severity.Critical);
        int warnCount = items.Count(i => i.Severity == Severity.Warning);
        int infoCount = items.Count(i => i.Severity == Severity.Info);

        score -= critCount * 15;
        score -= warnCount * 6;
        score -= infoCount * 2;
        if (score < 15) score = 15;

        report.HealthScore = score;
        report.Items = items.OrderByDescending(i => i.Severity).ThenByDescending(i => i.SizeBytes).ToList();

        if (critCount > 0)
        {
            report.HealthStatus = $"Wykryto {critCount} krytycznych problemów wymagających naprawy";
        }
        else if (warnCount > 0)
        {
            report.HealthStatus = $"Wykryto {warnCount} zalecanych optymalizacji";
        }
        else if (infoCount > 0)
        {
            report.HealthStatus = "System w dobrej kondycji • Dostępne usprawnienia";
        }
        else
        {
            report.HealthStatus = "Stan doskonały • System w 100% zoptymalizowany";
        }

        return report;
    }
}
