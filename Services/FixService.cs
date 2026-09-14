using DiskOptimizer.Models;
namespace DiskOptimizer.Services;
public class FixService : IFixService
{
    private readonly IHistoryService _history;
    private readonly ISystemRestoreService _restore;
    public FixService(IHistoryService? historyService = null, ISystemRestoreService? restoreService = null)
    {
        _history = historyService ?? new HistoryService();
        _restore = restoreService ?? new SystemRestoreService();
    }
    public async Task<FixReport> FixSelectedAsync(IEnumerable<ScanResultItem> items, IProgress<(int percent, string message)>? progress = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ct.ThrowIfCancellationRequested();
        var selected = items.Where(i => i.IsSelected).DistinctBy(i => i.Id).ToList();
        var report = new FixReport();
        if (selected.Count == 0) return report;
        try
        {
            // Never derive deletion targets or commands from mutable result rows.
            var catalog = await Task.Run(() => new DiskScannerService().GetDefaultItems(), ct).ConfigureAwait(false);
            var eligible = selected.Where(i => i.CanFix && i.Fix == FixAction.CleanFiles && catalog.Any(c => c.Id == i.Id && c.CanClean && c.ActionType == CleanActionType.DeleteFiles && string.Equals(c.Path, i.DetailPath, StringComparison.OrdinalIgnoreCase))).ToList();
            foreach (var skipped in selected.Except(eligible))
            {
                skipped.Status = "Pominięto: wymaga osobnego narzędzia lub ponownego skanu";
                report.SkippedCount++;
                report.Messages.Add($"{skipped.Name}: {skipped.Status}");
            }
            if (eligible.Count > 0)
            {
                progress?.Report((0, "Tworzenie punktu przywracania (nie jest kopią usuwanych plików)…"));
                var restore = await _restore.CreateRestorePointAsync(ct: ct).ConfigureAwait(false);
                report.Messages.Add(restore.message);
                if (!restore.success)
                {
                    report.FailureCount += eligible.Count;
                    foreach (var item in eligible) item.Status = "Nie wykonano: punkt przywracania nie został utworzony";
                }
                else
                {
                    for (int index = 0; index < eligible.Count; index++)
                    {
                        ct.ThrowIfCancellationRequested();
                        var item = eligible[index];
                        var target = catalog.Single(c => c.Id == item.Id);
                        target.SizeBytes = item.SizeBytes;
                        progress?.Report((index * 100 / eligible.Count, $"Czyszczenie: {item.Name}"));
                        try
                        {
                            var result = await Task.Run(() => new DiskCleanerService().CleanItemAsync(target, ct: ct), ct).ConfigureAwait(false);
                            report.BytesFreed += result.totalFreed;
                            ct.ThrowIfCancellationRequested();
                            if (target.Status.StartsWith("Błąd", StringComparison.OrdinalIgnoreCase)) throw new IOException(target.Status);
                            // A zero measurement is not evidence of a successful repair.
                            if (result.totalFreed == 0 && result.totalFiles == 0)
                            {
                                report.SkippedCount++;
                                item.Status = "Nie usunięto plików (zajęte, chronione lub już usunięte)";
                                item.StatusColor = "#F59E0B";
                            }
                            else
                            {
                                report.SuccessCount++;
                                item.Status = $"Usunięto {result.totalFiles} plików: {DriveModel.FormatBytes(result.totalFreed)}. Pozostałe wymagają ponownego skanu.";
                                item.StatusColor = "#10B981";
                                item.IsSelected = false;
                            }
                            report.Messages.Add($"{item.Name}: {item.Status}");
                        }
                        catch (OperationCanceledException) { item.Status = "Anulowano; część plików mogła zostać usunięta"; throw; }
                        catch (Exception ex)
                        {
                            report.FailureCount++;
                            item.Status = $"Błąd: {ex.Message}";
                            item.StatusColor = "#EF4444";
                            report.Messages.Add($"{item.Name}: {item.Status}");
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            report.WasCancelled = true;
            report.Messages.Add("Anulowano. Wykonane usunięcia nie zostały cofnięte; uruchom ponowny skan.");
        }
        catch (Exception ex)
        {
            report.FailureCount += Math.Max(1, selected.Count - report.SuccessCount - report.FailureCount - report.SkippedCount);
            report.Messages.Add($"Operacja nie została ukończona: {ex.Message}");
        }
        var summary = $"Wykonano: {report.SuccessCount}; błędy: {report.FailureCount}; pominięto: {report.SkippedCount}; zmierzone zwolnione miejsce: {DriveModel.FormatBytes(report.BytesFreed)}.";
        try
        {
            // Persist completed work even if caller cancelled.
            await _history.AddEntryAsync(new HistoryEntry
            {
                OperationType = "Czyszczenie zaznaczonych pozycji", ItemsFixed = report.SuccessCount,
                BytesSaved = report.BytesFreed, Success = !report.WasCancelled && report.FailureCount == 0 && report.SkippedCount == 0,
                Summary = (report.WasCancelled ? "Anulowano. " : "") + summary + " " + string.Join(" | ", report.Messages)
            }).ConfigureAwait(false);
        }
        catch (Exception ex) { report.Messages.Add($"Nie zapisano historii: {ex.Message}"); }
        progress?.Report((report.WasCancelled ? 0 : 100, summary));
        return report;
    }
}
