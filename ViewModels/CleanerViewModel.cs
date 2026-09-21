using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DiskOptimizer.Models;

namespace DiskOptimizer.ViewModels;

public enum CleanerState { Idle, Scanning, Results, Cleaning, Done, Error }

public sealed class CleanerViewModel : ObservableObject, IDisposable
{
    private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    private ObservableCollection<CleanItem> items = new();
    private readonly HashSet<CleanItem> subscriptions = new();
    private CleanerState state;
    private bool foundOnly = true;
    private bool cancelled;
    private bool scanCancelled;
    private bool disposed;
    private long outcomeBytes;
    private string result = "";
    private DateTimeOffset? scannedAt;

    public ICollectionView ItemsView { get; private set; }
    public CleanerState State
    {
        get => state;
        private set { if (SetProperty(ref state, value)) Refresh(); }
    }
    public bool FoundOnly
    {
        get => foundOnly;
        set
        {
            if (!SetProperty(ref foundOnly, value)) return;
            OnPropertyChanged(nameof(ShowAllCategories));
            ItemsView.Refresh();
        }
    }
    public bool ShowAllCategories { get => !FoundOnly; set => FoundOnly = !value; }
    public bool ShowIdle => State == CleanerState.Idle;
    public string IdleDescription => scanCancelled
        ? "Przerwano skanowanie. Możesz rozpocząć je ponownie."
        : "Sprawdź pliki tymczasowe i pamięć podręczną aplikacji.";
    public bool ShowResults => State == CleanerState.Results;
    public bool ShowOutcome => State is CleanerState.Done or CleanerState.Error;
    public bool IsCleaning => State == CleanerState.Cleaning;
    public bool IsBusy => State is CleanerState.Scanning or CleanerState.Cleaning;
    public bool CanScan => !IsBusy;
    public bool CanSelect => ShowResults;
    public bool HasCleanableData => items.Any(IsMeasuredCleanableItem);
    public bool CanClean => ShowResults && scannedAt.HasValue && items.Any(i => i.IsSelected && IsMeasuredCleanableItem(i));
    public string ResultsHeading => items.Any(i => !i.IsScanned)
        ? "Nie udało się odczytać wszystkich kategorii"
        : HasCleanableData ? "Możesz zwolnić" : "Nie znaleziono danych do usunięcia";
    public string Heading => State switch
    {
        CleanerState.Scanning => "Sprawdzanie plików",
        CleanerState.Cleaning => "Czyszczenie",
        CleanerState.Done => "Gotowe",
        CleanerState.Error when cancelled => "Czyszczenie zatrzymane",
        CleanerState.Error => "Czyszczenie niepełne",
        CleanerState.Results => "Wybierz pliki do usunięcia",
        _ => "Zwolnij miejsce"
    };
    public string Description => IsBusy
        ? items.FirstOrDefault(i => i.IsScanning || i.IsCleaning)?.Title
          ?? (IsCleaning ? "Przygotowywanie czyszczenia…" : "Odczytywanie danych…")
        : "Przeskanuj komputer i wybierz pliki do usunięcia.";
    public string ScanLabel => scannedAt.HasValue ? "Skanuj ponownie" : "Skanuj";
    public string FoundSize
    {
        get
        {
            if (!scannedAt.HasValue && !IsBusy) return "—";
            long measured = items.Where(i => i.CanClean && i.IsScanned).Sum(i => i.SizeBytes);
            // A failed/incomplete read must not look like an empty cache.
            return measured == 0 && items.Any(i => i.CanClean && !i.IsScanned)
                ? "—" : DriveModel.FormatBytes(measured);
        }
    }
    public string ScanSummary => scannedAt.HasValue
        ? $"Ostatni skan: {scannedAt:dd.MM HH:mm}" + (items.Any(i => !i.IsScanned) ? " • pominięto nieodczytane kategorie" : "")
        : "Wynik pojawi się po skanowaniu";
    public string SelectionText => $"Zaznaczono {items.Count(i => i.IsSelected && IsMeasuredCleanableItem(i))} kategorii • {DriveModel.FormatBytes(items.Where(i => i.IsSelected && IsMeasuredCleanableItem(i)).Sum(i => i.SizeBytes))}";
    public string CleanLabel => $"Przejrzyj i wyczyść ({items.Count(i => i.IsSelected && IsMeasuredCleanableItem(i))})";
    public string ResultText { get => result; private set => SetProperty(ref result, value); }
    public string OutcomeSize => DriveModel.FormatBytes(outcomeBytes);
    private int CompletedCount => items.Count(i => i.IsScanned || !i.IsScanning && i.Status.StartsWith("Błąd", StringComparison.OrdinalIgnoreCase));
    public string ProgressText => IsCleaning ? "Możesz zatrzymać dalsze usuwanie." : $"Sprawdzono {CompletedCount} z {items.Count} kategorii";
    public double Progress => items.Count == 0 ? 0 : 100.0 * CompletedCount / items.Count;

    public CleanerViewModel()
    {
        ItemsView = CollectionViewSource.GetDefaultView(items);
        SetItems(items);
    }

    public void SetItems(ObservableCollection<CleanItem> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        items.CollectionChanged -= CollectionChanged;
        foreach (var item in subscriptions) item.PropertyChanged -= ItemChanged;
        subscriptions.Clear();
        items = source;
        items.CollectionChanged += CollectionChanged;
        scannedAt = null;
        cancelled = false;
        scanCancelled = false;
        ResultText = "";
        State = CleanerState.Idle;
        ItemsView = CollectionViewSource.GetDefaultView(items);
        using (ItemsView.DeferRefresh())
        {
            ItemsView.GroupDescriptions.Clear();
            ItemsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(CleanItem.Category)));
            ItemsView.Filter = item => item is CleanItem clean && (!FoundOnly || !clean.IsScanned || clean.SizeBytes > 0);
        }
        Subscribe();
        OnPropertyChanged(nameof(ItemsView));
        Refresh();
    }

    public void SetScanState(bool active)
    {
        if (active)
        {
            scannedAt = null;
            cancelled = false;
            scanCancelled = false;
            ResultText = "";
            State = CleanerState.Scanning;
            foreach (var item in items)
            {
                item.IsScanned = false;
                item.IsScanning = false;
                item.Status = "Oczekuje na skanowanie";
            }
            Refresh();
        }
        else if (State == CleanerState.Scanning)
        {
            scanCancelled = !scannedAt.HasValue;
            State = scannedAt.HasValue ? CleanerState.Results : CleanerState.Idle;
        }
    }

    public void SetScanCompleted()
    {
        foreach (var item in items.Where(i => !IsMeasuredCleanableItem(i))) item.IsSelected = false;
        scannedAt = DateTimeOffset.Now;
        State = CleanerState.Results;
        Refresh(refreshItems: true);
    }

    public void SetCleaningState(bool active)
    {
        if (active) { cancelled = false; ResultText = ""; State = CleanerState.Cleaning; }
        else if (State == CleanerState.Cleaning) State = CleanerState.Results;
    }

    public void SetResult(long freed, int count, int errors, bool cancelled = false)
    {
        outcomeBytes = Math.Max(0, freed);
        this.cancelled = cancelled;
        ResultText = count > 0 ? $"Usunięte pliki: {count}." : outcomeBytes > 0 ? "Zwolniono pamięć podręczną." : "Nie zmierzono zwolnionego miejsca.";
        if (cancelled) ResultText += " Wykonanych usunięć nie cofnięto.";
        if (errors > 0) ResultText += $" Błędy lub pominięcia: {errors}. Szczegóły znajdziesz w historii.";
        State = cancelled || errors > 0 ? CleanerState.Error : CleanerState.Done;
        Refresh();
    }

    public void DeselectAll() { foreach (var item in items) item.IsSelected = false; }
    private static bool IsMeasuredCleanableItem(CleanItem item) => item.IsScanned && item.CanClean && item.SizeBytes > 0;
    private void Subscribe() { foreach (var item in items) if (subscriptions.Add(item)) item.PropertyChanged += ItemChanged; }
    private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Dispatch(() =>
    {
        foreach (var item in subscriptions.Where(i => !items.Contains(i)).ToArray())
        {
            item.PropertyChanged -= ItemChanged;
            subscriptions.Remove(item);
        }
        Subscribe();
        Refresh(refreshItems: true);
    });
    private void ItemChanged(object? sender, PropertyChangedEventArgs e) => Dispatch(() =>
        Refresh(e.PropertyName is nameof(CleanItem.SizeBytes) or nameof(CleanItem.IsScanned)));
    private void Dispatch(Action action)
    {
        if (disposed) return;
        if (dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(new Action(() => { if (!disposed) action(); }));
    }
    private void Refresh(bool refreshItems = false)
    {
        foreach (var name in new[]
        {
            nameof(IsBusy), nameof(CanScan), nameof(CanSelect), nameof(CanClean), nameof(Heading), nameof(Description),
            nameof(ScanLabel), nameof(FoundSize), nameof(ScanSummary), nameof(SelectionText), nameof(CleanLabel), nameof(Progress),
            nameof(ShowIdle), nameof(IdleDescription), nameof(ShowResults), nameof(ShowOutcome), nameof(IsCleaning), nameof(HasCleanableData),
            nameof(ResultsHeading), nameof(OutcomeSize), nameof(ProgressText)
        }) OnPropertyChanged(name);
        // Selection only changes totals. Rebuilding the view here would close open row details.
        if (refreshItems && FoundOnly) ItemsView.Refresh();
    }

    public void Dispose()
    {
        disposed = true;
        items.CollectionChanged -= CollectionChanged;
        foreach (var item in subscriptions) item.PropertyChanged -= ItemChanged;
        subscriptions.Clear();
    }
}
