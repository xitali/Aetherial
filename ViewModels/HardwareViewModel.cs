using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiskOptimizer.Models;
using DiskOptimizer.Services;

namespace DiskOptimizer.ViewModels;

public sealed class HardwareViewModel : ObservableObject, IDisposable
{
    private readonly DriverUpdaterService inventory;
    private readonly DriverCatalogService catalog;
    private CancellationTokenSource? cancellation;
    private string status = "Sprawdź zgodność i wersje sterowników dla wykrytego sprzętu.";
    private string search = "";
    private string filter = "Wszystkie";
    private bool busy;
    public ObservableCollection<DriverAssessment> Items { get; } = new();
    public ICollectionView Results { get; }
    public string[] Filters { get; } = { "Wszystkie", "Aktualizacje", "Zweryfikowane", "Niezweryfikowane" };
    public string Search { get => search; set { if (SetProperty(ref search, value)) Results.Refresh(); } }
    public string Filter { get => filter; set { if (SetProperty(ref filter, value)) Results.Refresh(); } }
    public string Status { get => status; private set => SetProperty(ref status, value); }
    public bool IsBusy { get => busy; private set { if (SetProperty(ref busy, value)) ScanCommand.NotifyCanExecuteChanged(); } }
    public int UpdateCount => Items.Count(i => i.HasUpdate);
    public int VerifiedCount => Items.Count(i => i.Status is DriverAssessmentStatus.UpToDate or DriverAssessmentStatus.NewerInstalled or DriverAssessmentStatus.UpdateAvailable);
    public int UnknownCount => Items.Count - VerifiedCount;
    public int DeviceCount => Items.Count;
    public string CheckedAt => Items.Count == 0 ? "Jeszcze nie sprawdzono" : Items.Max(i => i.CheckedAt).ToLocalTime().ToString("dd.MM.yyyy HH:mm");
    public AsyncRelayCommand ScanCommand { get; }
    public IRelayCommand CancelCommand { get; }

    public HardwareViewModel(DriverUpdaterService inventory, DriverCatalogService catalog)
    {
        this.inventory = inventory;
        this.catalog = catalog;
        Results = CollectionViewSource.GetDefaultView(Items);
        Results.Filter = item => item is DriverAssessment row &&
            (string.IsNullOrWhiteSpace(Search) || (row.DeviceName + " " + row.DeviceId + " " + row.SourceName).Contains(Search, StringComparison.CurrentCultureIgnoreCase)) &&
            (Filter switch { "Aktualizacje" => row.HasUpdate, "Zweryfikowane" => row.Status is DriverAssessmentStatus.UpToDate or DriverAssessmentStatus.NewerInstalled or DriverAssessmentStatus.UpdateAvailable, "Niezweryfikowane" => row.Status is DriverAssessmentStatus.Unsupported or DriverAssessmentStatus.Unknown or DriverAssessmentStatus.Error, _ => true });
        ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsBusy);
        CancelCommand = new RelayCommand(() => cancellation?.Cancel());
    }

    public async Task ScanAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        cancellation = new CancellationTokenSource();
        Items.Clear();
        RefreshSummary();
        Status = "1 / 2 • Odczyt identyfikatorów sprzętu i zainstalowanych sterowników…";
        try
        {
            var devices = await inventory.GetConnectedPnpDevicesAsync(ct: cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            Status = $"2 / 2 • Sprawdzanie katalogów producentów dla {devices.Count} urządzeń…";
            var results = await catalog.AssessAsync(devices, ct: cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            LoadResults(results);
            Status = $"Sprawdzono {DeviceCount} urządzeń • {UpdateCount} nowszych wersji • {UnknownCount} wymaga osobnej weryfikacji.";
            if (UpdateCount > 0) Filter = "Aktualizacje";
        }
        catch (OperationCanceledException) { Status = "Skan anulowany. Nie wyciągnięto wniosków o aktualności sterowników."; }
        catch (Exception ex) { Status = $"Nie ukończono sprawdzenia: {ex.Message}"; }
        finally { IsBusy = false; cancellation.Dispose(); cancellation = null; }
    }

    public void LoadResults(IEnumerable<DriverAssessment> rows)
    {
        Items.Clear();
        foreach (var row in rows.OrderByDescending(x => x.HasUpdate).ThenBy(x => x.DeviceName)) Items.Add(row);
        RefreshSummary();
        Results.Refresh();
    }
    private void RefreshSummary()
    {
        OnPropertyChanged(nameof(DeviceCount)); OnPropertyChanged(nameof(UpdateCount));
        OnPropertyChanged(nameof(VerifiedCount)); OnPropertyChanged(nameof(UnknownCount)); OnPropertyChanged(nameof(CheckedAt));
    }
    public void Dispose() => cancellation?.Cancel();
}
