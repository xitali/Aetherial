using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using DiskOptimizer.Models;
using DiskOptimizer.Services;

namespace DiskOptimizer.ViewModels;

public sealed class ExplorerViewModel : ObservableObject, IDisposable
{
    private readonly FileSystemExplorerService service;
    private CancellationTokenSource? request;
    private string currentPath = "";
    private string status = "Wybierz wolumin lub folder.";
    private bool busy;
    public int LargeFileThresholdMb { get; set; } = 500;
    public ObservableCollection<FileSystemItem> Items { get; } = new();
    public string CurrentPath { get => currentPath; private set => SetProperty(ref currentPath, value); }
    public string Status { get => status; private set => SetProperty(ref status, value); }
    public bool IsBusy { get => busy; private set => SetProperty(ref busy, value); }
    public ExplorerViewModel(FileSystemExplorerService service) => this.service = service;

    public async Task NavigateAsync(string path, bool largeFiles = false)
    {
        request?.Cancel();
        var active = new CancellationTokenSource();
        request = active;
        Items.Clear();
        IsBusy = true;
        try
        {
            CurrentPath = DriveModel.NormalizePath(path);
            Status = largeFiles ? $"Szukanie dużych plików: {CurrentPath}" : $"Odczytywanie: {CurrentPath}";
            var result = largeFiles
                ? await service.FindLargeFilesAsync(CurrentPath, Math.Clamp(LargeFileThresholdMb, 100, 10240) * 1024L * 1024, ct: active.Token)
                : await service.GetFolderContentsAsync(CurrentPath, active.Token);
            active.Token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(request, active)) return;
            foreach (var item in result.OrderByDescending(i => i.IsDirectory).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)) Items.Add(item);
            Status = Items.Count == 0 ? "Brak elementów w tym widoku." : $"{Items.Count} elementów • {CurrentPath}";
            if (largeFiles && Items.Count >= 60) Status += " • limit 60 wyników";
        }
        catch (OperationCanceledException) { if (ReferenceEquals(request, active)) Status = "Odczyt anulowany."; }
        catch (Exception ex) { if (ReferenceEquals(request, active)) Status = $"Nie można odczytać folderu: {ex.Message}"; }
        finally
        {
            if (ReferenceEquals(request, active)) { IsBusy = false; request = null; }
            active.Dispose();
        }
    }
    public void Cancel() => request?.Cancel();
    public void Dispose() { request?.Cancel(); }
}
