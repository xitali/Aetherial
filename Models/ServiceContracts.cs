using DiskOptimizer.Models;
namespace DiskOptimizer.Services;
public class ScanReport
{
    // Compatibility property: measurement coverage, never a system health score.
    public int HealthScore => CheckedCount == 0 ? 0 : (int)(100L * (CheckedCount - Warnings.Count) / CheckedCount);
    public string HealthStatus => Warnings.Count > 0 ? $"Skan częściowy: {Warnings.Count} ostrzeżeń odczytu" : $"Zakończono skan: {Items.Count} pozycji do przejrzenia";
    public List<ScanResultItem> Items { get; set; } = new();
    public List<string> Warnings { get; } = new();
    public int CheckedCount { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public long TotalRecoverableBytes => Items.Where(i => i.CanFix).Sum(i => i.SizeBytes);
    public int ProblemCount => Items.Count;
}
public interface IScanService
{
    Task<ScanReport> ScanAllAsync(IProgress<(int percent, string message)>? progress = null, CancellationToken ct = default);
}
public class FixReport
{
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public int SkippedCount { get; set; }
    public bool WasCancelled { get; set; }
    public long BytesFreed { get; set; }
    public List<string> Messages { get; set; } = new();
}
public interface IFixService
{
    Task<FixReport> FixSelectedAsync(IEnumerable<ScanResultItem> items, IProgress<(int percent, string message)>? progress = null, CancellationToken ct = default);
}
public interface IHistoryService
{
    Task<List<HistoryEntry>> GetHistoryAsync(CancellationToken ct = default);
    Task AddEntryAsync(HistoryEntry entry, CancellationToken ct = default);
    Task ClearHistoryAsync(CancellationToken ct = default);
}
public interface ISystemRestoreService
{
    Task<(bool success, string message)> CreateRestorePointAsync(string description = "Aetherial - Przed czyszczeniem", CancellationToken ct = default);
}
