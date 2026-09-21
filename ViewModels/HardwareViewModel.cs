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
    private string status = "Sprawdź dostępność nowszych wersji dla swojego sprzętu.";
    private string search = "";
    private string filter = "Wszystkie";
    private bool busy;
    private bool scanAttempted;
    private bool scanCompleted;
    public ObservableCollection<DriverAssessment> Items { get; } = new();
    public ICollectionView Results { get; }
    public string[] Filters { get; } = { "Wszystkie", "Aktualizacje", "Zweryfikowane", "Bez potwierdzenia" };
    public string Search { get => search; set { if (SetProperty(ref search, value)) RefreshResults(); } }
    public string Filter { get => filter; set { if (SetProperty(ref filter, value)) RefreshResults(); } }
    public string Status { get => status; private set => SetProperty(ref status, value); }
    public bool IsBusy
    {
        get => busy;
        private set
        {
            if (!SetProperty(ref busy, value)) return;
            ScanCommand.NotifyCanExecuteChanged();
            RefreshEmptyState();
        }
    }
    public int UpdateCount => Items.Count(i => i.HasUpdate);
    public int VerifiedCount => Items.Count(i => i.Status is DriverAssessmentStatus.UpToDate or DriverAssessmentStatus.NewerInstalled or DriverAssessmentStatus.UpdateAvailable);
    public int UnknownCount => Items.Count - VerifiedCount;
    public int DeviceCount => Items.Count;
    public string CheckedAt => Items.Count == 0 ? "Jeszcze nie sprawdzono" : Items.Max(i => i.CheckedAt).ToLocalTime().ToString("dd.MM.yyyy HH:mm");
    public bool HasResults => Items.Count > 0;
    public bool ShowEmptyState => IsBusy || Results.IsEmpty;
    public string EmptyTitle => IsBusy ? "Sprawdzanie sterowników…"
        : HasResults ? "Brak wyników dla tego filtra"
        : scanCompleted ? "Nie znaleziono urządzeń"
        : scanAttempted ? "Sprawdzenie nie zostało ukończone"
        : "Gotowe do sprawdzenia";
    public string EmptyDescription => IsBusy ? "Możesz korzystać z pozostałych sekcji."
        : HasResults ? "Zmień kategorię lub wyszukiwaną nazwę."
        : scanCompleted ? "Podłącz urządzenie i spróbuj ponownie."
        : scanAttempted ? "Uruchom skan ponownie, aby uzyskać aktualne wyniki."
        : "Wykryj sprzęt i porównaj dostępne wersje.";
    public AsyncRelayCommand ScanCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand<string> SetFilterCommand { get; }

    public HardwareViewModel(DriverUpdaterService inventory, DriverCatalogService catalog)
    {
        this.inventory = inventory;
        this.catalog = catalog;
        Results = CollectionViewSource.GetDefaultView(Items);
        Results.Filter = item => item is DriverAssessment row &&
            (string.IsNullOrWhiteSpace(Search) || (row.DeviceName + " " + row.DeviceId + " " + row.SourceName).Contains(Search, StringComparison.CurrentCultureIgnoreCase)) &&
            (Filter switch { "Aktualizacje" => row.HasUpdate, "Zweryfikowane" => row.Status is DriverAssessmentStatus.UpToDate or DriverAssessmentStatus.NewerInstalled or DriverAssessmentStatus.UpdateAvailable, "Bez potwierdzenia" or "Niezweryfikowane" => row.Status is DriverAssessmentStatus.Unsupported or DriverAssessmentStatus.Unknown or DriverAssessmentStatus.Error, _ => true });
        ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsBusy);
        CancelCommand = new RelayCommand(() => cancellation?.Cancel());
        SetFilterCommand = new RelayCommand<string>(value => Filter = value ?? "Wszystkie");
    }

    public async Task ScanAsync()
    {
        if (IsBusy) return;
        scanAttempted = true;
        scanCompleted = false;
        IsBusy = true;
        cancellation = new CancellationTokenSource();
        Items.Clear();
        Search = "";
        RefreshSummary();
        Status = "Odczytywanie sprzętu i zainstalowanych wersji…";
        try
        {
            var devices = await inventory.GetConnectedPnpDevicesAsync(ct: cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            Status = "Porównywanie wersji z katalogiem producenta…";
            var results = await catalog.AssessAsync(devices, ct: cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            LoadResults(results);
        }
        catch (OperationCanceledException) { Status = "Sprawdzanie anulowane. Aktualność nie została potwierdzona."; }
        catch (Exception ex) { Status = $"Nie ukończono sprawdzenia: {ex.Message}"; }
        finally { IsBusy = false; cancellation.Dispose(); cancellation = null; }
    }

    public void LoadResults(IEnumerable<DriverAssessment> rows)
    {
        scanAttempted = true;
        scanCompleted = true;
        Items.Clear();
        foreach (var row in rows.OrderByDescending(x => x.HasUpdate).ThenBy(x => x.DeviceName)) Items.Add(row);
        Filter = UpdateCount > 0 ? "Aktualizacje" : VerifiedCount > 0 ? "Zweryfikowane" : "Wszystkie";
        Status = UpdateCount > 0 ? $"Dostępne aktualizacje: {UpdateCount}."
            : VerifiedCount > 0 ? "Brak nowszych wersji w sprawdzonym katalogu."
            : DeviceCount > 0 ? "Aktualność wykrytych sterowników wymaga potwierdzenia u producenta."
            : "Skan zakończony bez wykrytych urządzeń.";
        if (UnknownCount > 0 && VerifiedCount > 0) Status += $" Bez potwierdzenia: {UnknownCount}.";
        RefreshSummary();
        RefreshResults();
    }
    private void RefreshSummary()
    {
        OnPropertyChanged(nameof(DeviceCount)); OnPropertyChanged(nameof(UpdateCount));
        OnPropertyChanged(nameof(VerifiedCount)); OnPropertyChanged(nameof(UnknownCount)); OnPropertyChanged(nameof(CheckedAt));
        OnPropertyChanged(nameof(HasResults));
        RefreshEmptyState();
    }
    private void RefreshResults() { Results.Refresh(); RefreshEmptyState(); }
    private void RefreshEmptyState()
    {
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptyDescription));
    }
    public void Dispose() => cancellation?.Cancel();
}
