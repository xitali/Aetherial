using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DiskOptimizer.Models;

namespace DiskOptimizer.Services;

public interface IHistoryService
{
    Task<List<HistoryEntry>> GetHistoryAsync();
    Task AddEntryAsync(HistoryEntry entry);
    Task ClearHistoryAsync();
}

public class HistoryService : IHistoryService
{
    private readonly string _historyFilePath;
    private readonly object _lock = new();

    public HistoryService()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appFolder = Path.Combine(localAppData, "Aetherial");
        Directory.CreateDirectory(appFolder);
        _historyFilePath = Path.Combine(appFolder, "history.json");
    }

    public async Task<List<HistoryEntry>> GetHistoryAsync()
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                if (!File.Exists(_historyFilePath)) return new List<HistoryEntry>();
                try
                {
                    string json = File.ReadAllText(_historyFilePath);
                    var list = JsonSerializer.Deserialize<List<HistoryEntry>>(json);
                    return list?.OrderByDescending(x => x.Timestamp).ToList() ?? new List<HistoryEntry>();
                }
                catch
                {
                    return new List<HistoryEntry>();
                }
            }
        });
    }

    public async Task AddEntryAsync(HistoryEntry entry)
    {
        await Task.Run(() =>
        {
            lock (_lock)
            {
                var list = new List<HistoryEntry>();
                if (File.Exists(_historyFilePath))
                {
                    try
                    {
                        string existing = File.ReadAllText(_historyFilePath);
                        list = JsonSerializer.Deserialize<List<HistoryEntry>>(existing) ?? new List<HistoryEntry>();
                    }
                    catch { }
                }

                list.Insert(0, entry);
                if (list.Count > 100) list = list.Take(100).ToList();

                try
                {
                    string json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
                    string tempPath = _historyFilePath + ".tmp";
                    File.WriteAllText(tempPath, json);
                    File.Move(tempPath, _historyFilePath, true);
                }
                catch { }
            }
        });
    }

    public async Task ClearHistoryAsync()
    {
        await Task.Run(() =>
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_historyFilePath)) File.Delete(_historyFilePath);
                }
                catch { }
            }
        });
    }
}
