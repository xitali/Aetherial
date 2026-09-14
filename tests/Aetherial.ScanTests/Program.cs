using System.Text.Json;
using DiskOptimizer.Models;
using DiskOptimizer.Services;

var root = Path.Combine(Path.GetTempPath(), "Aetherial.ScanTests", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var historyPath = Path.Combine(root, "history.json");
var history = new HistoryService(historyPath);
int checks = 0;
void Check(bool pass, string description) { if (!pass) throw new Exception(description); checks++; Console.WriteLine("PASS " + description); }
Check((await history.GetHistoryAsync()).Count == 0, "Missing journal reads empty");
await Task.WhenAll(Enumerable.Range(0, 120).Select(i => new HistoryService(historyPath).AddEntryAsync(new HistoryEntry { Summary = i.ToString() })));
var entries = await history.GetHistoryAsync();
Check(entries.Count == 100, "Concurrent instances preserve bounded journal");
Check(entries.Select(x => x.Id).Distinct().Count() == 100, "No duplicate history entries");
Check(!Directory.EnumerateFiles(root, "*.tmp").Any(), "Atomic writes leave no temp files");
using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
try { await history.AddEntryAsync(new HistoryEntry(), cancellation.Token); throw new Exception("No cancellation"); } catch (OperationCanceledException) { checks++; }
Check((await history.GetHistoryAsync()).Count == 100, "Cancelled write preserves journal");
await File.WriteAllTextAsync(historyPath, "broken json");
try { await history.AddEntryAsync(new HistoryEntry()); throw new Exception("Corrupt history accepted"); } catch (JsonException) { checks++; }
Check(await File.ReadAllTextAsync(historyPath) == "broken json", "Corrupt journal not overwritten");
await history.ClearHistoryAsync();
Check((await history.GetHistoryAsync()).Count == 0, "Explicit clear removes journal");
var fixes = new FixService(history, new ForbiddenRestore());
var report = await fixes.FixSelectedAsync(new[] { new ScanResultItem { Id = "fake", Name = "Driver", Fix = FixAction.UpdateDriver, IsSelected = true } });
Check(report.SuccessCount == 0 && report.SkippedCount == 1, "Driver instruction is not a completed repair");
Check(!(await history.GetHistoryAsync())[0].Success, "Skipped repair does not log success");
var candidate = new DiskScannerService().GetDefaultItems().First(x => x.CanClean && x.ActionType == CleanActionType.DeleteFiles);
var refused = await new FixService(history, new RefusedRestore()).FixSelectedAsync(new[] { new ScanResultItem { Id = candidate.Id, Name = candidate.Title, DetailPath = candidate.Path, Fix = FixAction.CleanFiles, CanFix = true, IsSelected = true } });
Check(refused.SuccessCount == 0 && refused.FailureCount == 1, "Restore failure prevents file deletion");
try { await new ScanService().ScanAllAsync(ct: cancellation.Token); throw new Exception("No cancellation"); } catch (OperationCanceledException) { checks++; }
Check(new ScanReport().HealthScore == 0, "Unscanned report never claims perfect health");
var tampered = await fixes.FixSelectedAsync(new[] { new ScanResultItem { Id = candidate.Id, Name = candidate.Title, DetailPath = root, Fix = FixAction.CleanFiles, CanFix = true, IsSelected = true } });
Check(tampered.SuccessCount == 0 && tampered.SkippedCount == 1, "Modified result path cannot redirect deletion");
var interrupted = await new FixService(history, new CancelledRestore()).FixSelectedAsync(new[] { new ScanResultItem { Id = candidate.Id, Name = candidate.Title, DetailPath = candidate.Path, Fix = FixAction.CleanFiles, CanFix = true, IsSelected = true } });
Check(interrupted.WasCancelled && interrupted.SuccessCount == 0, "Cancellation yields partial report without success");
Check(!(await history.GetHistoryAsync())[0].Success, "Cancellation saved to history as incomplete");
Console.WriteLine($"{checks} checks passed; isolated journal at {historyPath}");
sealed class ForbiddenRestore : ISystemRestoreService
{
    public Task<(bool success, string message)> CreateRestorePointAsync(string description = "", CancellationToken ct = default) => throw new Exception("Restore called for unsupported repair");
}
sealed class RefusedRestore : ISystemRestoreService
{
    public Task<(bool success, string message)> CreateRestorePointAsync(string description = "", CancellationToken ct = default) => Task.FromResult((false, "Test odmowy; bez zmian systemowych"));
}
sealed class CancelledRestore : ISystemRestoreService
{
    public Task<(bool success, string message)> CreateRestorePointAsync(string description = "", CancellationToken ct = default) => throw new OperationCanceledException();
}
