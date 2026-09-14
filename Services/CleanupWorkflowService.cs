using DiskOptimizer.Models;

namespace DiskOptimizer.Services;

public sealed class CleanupWorkflowService(DiskScannerService scanner, DiskCleanerService cleaner, IHistoryService history, ISystemRestoreService restore)
{
    public async Task<CleanupReport> ExecuteAsync(IReadOnlyList<CleanItem> selected, Action<string>? log, CancellationToken ct)
    {
        long bytes = 0; int files = 0; int errors = 0; bool cancelled = false;
        try
        {
            var current = scanner.GetDefaultItems();
            if (selected.Any(row => !row.IsScanned || !current.Any(item => item.Id == row.Id && item.Path == row.Path && item.ActionType == row.ActionType && item.CanClean)))
                throw new InvalidOperationException("Zakres nie odpowiada bieżącemu katalogowi. Wykonaj ponowny skan.");
            var point = await restore.CreateRestorePointAsync("Aetherial — przed czyszczeniem", ct);
            log?.Invoke(point.message);
            if (!point.success) throw new InvalidOperationException("Nie utworzono punktu przywracania. Nie usunięto plików.");
            foreach (var row in selected)
            {
                ct.ThrowIfCancellationRequested();
                // Execute known targets/commands from the current catalog, not mutable UI data.
                var target = current.Single(item => item.Id == row.Id);
                target.SizeBytes = row.SizeBytes;
                row.IsCleaning = true;
                try
                {
                    var result = await cleaner.CleanItemAsync(target, log, ct);
                    bytes += result.totalFreed; files += result.totalFiles;
                    if (target.Status.StartsWith("Błąd", StringComparison.OrdinalIgnoreCase) || result is (0, 0)) errors++;
                    row.Status = target.Status;
                    await scanner.ScanItemAsync(row, ct);
                    row.IsSelected = false;
                }
                finally { row.IsCleaning = false; }
            }
        }
        catch (OperationCanceledException) { cancelled = true; log?.Invoke("Anulowano. Wykonanych usunięć nie cofnięto; pomiar częściowy."); }
        catch (Exception ex) { errors++; log?.Invoke(ex.Message); }
        string summary = $"{(cancelled ? "Anulowano" : "Zakończono")}: {files} plików, zmierzono {DriveModel.FormatBytes(bytes)}, błędy/pominięcia: {errors}.";
        try
        {
            await history.AddEntryAsync(new HistoryEntry { OperationType = "Czyszczenie", BytesSaved = bytes, ItemsFixed = files, Success = !cancelled && errors == 0, Summary = summary }, CancellationToken.None);
        }
        catch (Exception ex) { errors++; summary += $" Nie zapisano historii: {ex.Message}"; }
        return new(bytes, files, errors, cancelled, summary);
    }
}
