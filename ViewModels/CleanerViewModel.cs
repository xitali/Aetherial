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
    private bool foundOnly;
    private string result = "";
    private DateTimeOffset? scannedAt;
    public ICollectionView ItemsView { get; private set; }
    public CleanerState State { get => state; private set { if (SetProperty(ref state, value)) Refresh(); } }
    public bool FoundOnly { get => foundOnly; set { if (SetProperty(ref foundOnly, value)) ItemsView.Refresh(); } }
    public bool IsBusy => State is CleanerState.Scanning or CleanerState.Cleaning;
    public bool CanScan => !IsBusy;
    public bool CanSelect => !IsBusy;
    public bool CanClean => !IsBusy && scannedAt.HasValue && items.Any(i => i.IsSelected && i.CanClean && i.SizeBytes > 0 && i.IsScanned);
    public string Heading => State switch { CleanerState.Scanning => "Sprawdzanie pamięci podręcznych", CleanerState.Cleaning => "Czyszczenie wybranych pozycji", CleanerState.Done => "Operacja zakończona", CleanerState.Error => "Sprawdź wynik operacji", CleanerState.Results => "Wybierz, co chcesz usunąć", _ => "Zwolnij miejsce z kontrolą" };
    public string Description => IsBusy ? items.FirstOrDefault(i => i.IsScanning || i.IsCleaning)?.Title ?? "Odczytywanie bieżącego stanu…" : "Przeskanuj komputer, przejrzyj wyniki i potwierdź usunięcie wybranych plików.";
    public string ScanLabel => scannedAt.HasValue ? "Skanuj ponownie" : "Skanuj pamięci podręczne";
    public string FoundSize => scannedAt.HasValue || IsBusy ? DriveModel.FormatBytes(items.Where(i => i.CanClean && i.IsScanned).Sum(i => i.SizeBytes)) : "—";
    public string ScanSummary => scannedAt.HasValue ? $"Ostatni skan: {scannedAt:dd.MM HH:mm} • {items.Count(i => i.IsScanned && i.SizeBytes > 0)} kategorii z danymi" : "Rozmiar zostanie odczytany podczas skanowania";
    public string SelectionText => $"Zaznaczono {items.Count(i => i.IsSelected && i.CanClean && i.IsScanned && i.SizeBytes > 0)} kategorii • {DriveModel.FormatBytes(items.Where(i => i.IsSelected && i.CanClean && i.IsScanned).Sum(i => i.SizeBytes))}";
    public string CleanLabel => $"Przejrzyj i wyczyść ({items.Count(i => i.IsSelected && i.CanClean && i.IsScanned && i.SizeBytes > 0)})";
    public string ResultText { get => result; private set => SetProperty(ref result, value); }
    public double Progress => items.Count == 0 ? 0 : 100.0 * items.Count(i => i.IsScanned) / items.Count;

    public CleanerViewModel() { ItemsView = CollectionViewSource.GetDefaultView(items); SetItems(items); }
    public void SetItems(ObservableCollection<CleanItem> source)
    {
        items.CollectionChanged -= CollectionChanged;
        foreach (var item in subscriptions) item.PropertyChanged -= ItemChanged;
        subscriptions.Clear();
        items = source;
        items.CollectionChanged += CollectionChanged;
        ItemsView = CollectionViewSource.GetDefaultView(items);
        using (ItemsView.DeferRefresh())
        {
            ItemsView.GroupDescriptions.Clear();
            ItemsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(CleanItem.Category)));
            ItemsView.Filter = item => !FoundOnly || item is CleanItem clean && clean.IsScanned && clean.SizeBytes > 0;
        }
        Subscribe();
        OnPropertyChanged(nameof(ItemsView));
        Refresh();
    }
    public void SetScanState(bool active) { if (active) { scannedAt = null; ResultText = ""; State = CleanerState.Scanning; foreach (var item in items) item.IsScanned = false; } else if (State == CleanerState.Scanning) State = scannedAt.HasValue ? CleanerState.Results : CleanerState.Idle; }
    public void SetScanCompleted() { scannedAt = DateTimeOffset.Now; State = CleanerState.Results; Refresh(); }
    public void SetCleaningState(bool active) { if (active) { ResultText = ""; State = CleanerState.Cleaning; } else if (State == CleanerState.Cleaning) State = CleanerState.Results; }
    public void SetResult(long freed, int count, int errors) { ResultText = $"Zwolniono {DriveModel.FormatBytes(freed)} • usunięto {count} plików • błędy lub pominięcia: {errors}."; State = errors > 0 ? CleanerState.Error : CleanerState.Done; }
    public void DeselectAll() { foreach (var item in items) item.IsSelected = false; }
    private void Subscribe() { foreach (var item in items) if (subscriptions.Add(item)) item.PropertyChanged += ItemChanged; }
    private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Dispatch(() => { foreach (var item in subscriptions.Where(i => !items.Contains(i)).ToArray()) { item.PropertyChanged -= ItemChanged; subscriptions.Remove(item); } Subscribe(); Refresh(); });
    private void ItemChanged(object? sender, PropertyChangedEventArgs e) => Dispatch(Refresh);
    private void Dispatch(Action action) { if (dispatcher.CheckAccess()) action(); else dispatcher.BeginInvoke(action); }
    private void Refresh()
    {
        foreach (var name in new[] { nameof(IsBusy), nameof(CanScan), nameof(CanSelect), nameof(CanClean), nameof(Heading), nameof(Description), nameof(ScanLabel), nameof(FoundSize), nameof(ScanSummary), nameof(SelectionText), nameof(CleanLabel), nameof(Progress) }) OnPropertyChanged(name);
        if (FoundOnly) ItemsView.Refresh();
    }
    public void Dispose() { items.CollectionChanged -= CollectionChanged; foreach (var item in subscriptions) item.PropertyChanged -= ItemChanged; subscriptions.Clear(); }
}
