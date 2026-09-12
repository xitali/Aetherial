using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiskOptimizer.Models;

namespace DiskOptimizer.Services;

public class FixReport
{
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public long BytesFreed { get; set; }
    public List<string> Messages { get; set; } = new();
}

public interface IFixService
{
    Task<FixReport> FixSelectedAsync(IEnumerable<ScanResultItem> items, IProgress<(int percent, string message)>? progress = null, CancellationToken ct = default);
}

public class FixService : IFixService
{
    private readonly DiskCleanerService _cleaner = new();
    private readonly MemoryOptimizerService _ramOptimizer = new();
    private readonly IHistoryService _historyService;

    public FixService(IHistoryService? historyService = null)
    {
        _historyService = historyService ?? new HistoryService();
    }

    public async Task<FixReport> FixSelectedAsync(IEnumerable<ScanResultItem> items, IProgress<(int percent, string message)>? progress = null, CancellationToken ct = default)
    {
        var report = new FixReport();
        var selectedList = items.Where(i => i.IsSelected).ToList();
        int total = selectedList.Count;
        if (total == 0)
        {
            progress?.Report((100, "Brak zaznaczonych pozycji do naprawy."));
            return report;
        }

        int current = 0;

        foreach (var item in selectedList)
        {
            if (ct.IsCancellationRequested) break;
            current++;
            int pct = (int)((double)current / total * 100);
            progress?.Report((pct, $"Naprawianie ({current}/{total}): {item.Name}..."));

            try
            {
                switch (item.Fix)
                {
                    case FixAction.CleanFiles:
                        var cleanItem = new CleanItem
                        {
                            Id = item.Id,
                            Title = item.Name,
                            Path = item.DetailPath
                        };
                        var (freedBytes, removedFiles) = await _cleaner.CleanItemAsync(cleanItem, ct: ct);
                        report.BytesFreed += freedBytes;
                        report.SuccessCount++;
                        item.Status = $"Oczyszczono ({DriveModel.FormatBytes(freedBytes)})";
                        item.StatusColor = "#10B981";
                        report.Messages.Add($"Oczyszczono: {item.Name} — zwolniono {DriveModel.FormatBytes(freedBytes)} ({removedFiles} plików).");
                        break;

                    case FixAction.OptimizeRam:
                        var (ramFreed, procs) = await _ramOptimizer.OptimizeRamAsync(ct: ct);
                        report.BytesFreed += ramFreed;
                        report.SuccessCount++;
                        item.Status = "Zoptymalizowano RAM";
                        item.StatusColor = "#10B981";
                        report.Messages.Add($"Pamięć RAM: zwolniono {DriveModel.FormatBytes(ramFreed)} w {procs} procesach.");
                        break;

                    case FixAction.TrimSsd:
                        var (trimOk, _, _) = await DiskHelper.RunPowerShellScriptAsync("Get-Volume | Where-Object { $_.DriveType -eq 'Fixed' -and $_.DriveLetter } | ForEach-Object { Optimize-Volume -DriveLetter $_.DriveLetter -ReTrim }");
                        if (trimOk)
                        {
                            report.SuccessCount++;
                            item.Status = "Wykonano ReTrim";
                            item.StatusColor = "#10B981";
                            report.Messages.Add("Wykonano procedurę ReTrim dla wszystkich woluminów SSD.");
                        }
                        else
                        {
                            report.FailureCount++;
                            item.Status = "Pominięto (brak uprawnień)";
                            item.StatusColor = "#F59E0B";
                        }
                        break;

                    case FixAction.UpdateDriver:
                        item.Status = "Otwórz Menedżer Urządzeń";
                        item.StatusColor = "#38BDF8";
                        report.SuccessCount++;
                        report.Messages.Add($"Wymaga aktualizacji sterownika: {item.Name}.");
                        break;

                    default:
                        report.SuccessCount++;
                        item.Status = "Przetworzono";
                        item.StatusColor = "#10B981";
                        break;
                }
            }
            catch (Exception ex)
            {
                report.FailureCount++;
                item.Status = "Błąd";
                item.StatusColor = "#EF4444";
                report.Messages.Add($"Błąd przy {item.Name}: {ex.Message}");
            }
        }

        // Zapis do historii operacji
        try
        {
            var historyEntry = new HistoryEntry
            {
                OperationType = "Optymalizacja 1-Kliknięciem",
                ItemsFixed = report.SuccessCount,
                BytesSaved = report.BytesFreed,
                Success = report.FailureCount == 0,
                Summary = $"Naprawiono {report.SuccessCount} pozycji, zwolniono {DriveModel.FormatBytes(report.BytesFreed)}."
            };
            await _historyService.AddEntryAsync(historyEntry);
        }
        catch { }

        progress?.Report((100, $"Zakończono naprawę! Zwolniono: {DriveModel.FormatBytes(report.BytesFreed)}."));
        return report;
    }
}
