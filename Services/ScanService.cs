using DiskOptimizer.Models;
namespace DiskOptimizer.Services;

public class ScanService : IScanService
{
    public async Task<ScanReport> ScanAllAsync(IProgress<(int percent, string message)>? progress = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return await Task.Run(async () =>
        {
            var scanner = new DiskScannerService();
            var report = new ScanReport();
            var candidates = scanner.GetDefaultItems();
            foreach (var candidate in candidates)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report((report.CheckedCount * 100 / Math.Max(1, candidates.Count), $"Sprawdzanie: {candidate.Title}"));
                try
                {
                    await scanner.ScanItemAsync(candidate, ct).ConfigureAwait(false);
                    ct.ThrowIfCancellationRequested();
                    if (candidate.Status.StartsWith("Błąd", StringComparison.OrdinalIgnoreCase))
                        report.Warnings.Add($"{candidate.Title}: {candidate.Status}");
                    else if (candidate.SizeBytes > 0)
                    {
                        // Cache size is not evidence of a fault; only ordinary cache deletion is supported here.
                        bool canFix = candidate.CanClean && candidate.ActionType == CleanActionType.DeleteFiles;
                        report.Items.Add(new ScanResultItem
                        {
                            Id = candidate.Id, Name = candidate.Title, Description = candidate.Description,
                            Category = candidate.Category, SizeBytes = candidate.SizeBytes, DetailPath = candidate.Path,
                            Severity = Severity.Info, Fix = canFix ? FixAction.CleanFiles : FixAction.Generic,
                            CanFix = canFix, IsSelected = false,
                            Status = canFix ? "Do przejrzenia" : "Obsługa w module Czyść"
                        });
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { report.Warnings.Add($"{candidate.Title}: {ex.Message}"); }
                report.CheckedCount++;
            }
            report.Items = report.Items.OrderBy(i => i.Severity).ThenByDescending(i => i.SizeBytes).ToList();
            report.CompletedAt = DateTimeOffset.Now;
            progress?.Report((100, report.HealthStatus));
            return report;
        }, ct).ConfigureAwait(false);
    }
}
