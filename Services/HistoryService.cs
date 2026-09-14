using System.Collections.Concurrent;
using System.Text.Json;
using DiskOptimizer.Models;
namespace DiskOptimizer.Services;
public class HistoryService : IHistoryService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _path;
    private readonly SemaphoreSlim _gate;
    public HistoryService(string? historyFilePath = null)
    {
        _path = Path.GetFullPath(historyFilePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aetherial", "history.json"));
        _gate = Gates.GetOrAdd(_path, _ => new SemaphoreSlim(1, 1));
    }
    private async Task<List<HistoryEntry>> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(_path)) return new();
        var json = await File.ReadAllTextAsync(_path, ct).ConfigureAwait(false);
        // Corruption is reported; never silently erase an unreadable journal on append.
        return (JsonSerializer.Deserialize<List<HistoryEntry>>(json) ?? throw new JsonException("Nieprawidłowy dziennik historii."))
            .OrderByDescending(x => x.Timestamp).Take(100).ToList();
    }
    public async Task<List<HistoryEntry>> GetHistoryAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try { return await ReadAsync(ct).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }
    public async Task AddEntryAsync(HistoryEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        string temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var entries = await ReadAsync(ct).ConfigureAwait(false);
            entries.Insert(0, entry);
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(entries.Take(100)), ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (File.Exists(_path)) File.Replace(temp, _path, null);
            else File.Move(temp, _path);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); }
            finally { _gate.Release(); }
        }
    }
    public async Task ClearHistoryAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try { ct.ThrowIfCancellationRequested(); File.Delete(_path); }
        finally { _gate.Release(); }
    }
}
