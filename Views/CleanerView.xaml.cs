using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using DiskOptimizer.Models;
using DiskOptimizer.ViewModels;

namespace DiskOptimizer.Views;

public partial class CleanerView : UserControl
{
    public CleanerViewModel ViewModel { get; } = new();
    public ItemsControl ItemsList => ResultsList;
    public TextBlock SelectionSummary => SelectedSummary;
    public event EventHandler? ScanRequested;
    public event EventHandler? CleanRequested;
    public event EventHandler? CancelRequested;
    private void Cancel_Click(object sender, RoutedEventArgs e) => CancelRequested?.Invoke(this, EventArgs.Empty);
    public CleanerView() { InitializeComponent(); DataContext = ViewModel; }
    public void SetItems(ObservableCollection<CleanItem> items) => ViewModel.SetItems(items);
    public void SetScanState(bool active) => ViewModel.SetScanState(active);
    public void SetScanCompleted() => ViewModel.SetScanCompleted();
    public void SetCleaningState(bool active) => ViewModel.SetCleaningState(active);
    public void SetResult(long freed, int count, int errors, bool cancelled = false) => ViewModel.SetResult(freed, count, errors, cancelled);
    private void Scan_Click(object sender, RoutedEventArgs e) { if (ViewModel.CanScan) ScanRequested?.Invoke(this, EventArgs.Empty); }
    private void Clean_Click(object sender, RoutedEventArgs e) { if (ViewModel.CanClean) CleanRequested?.Invoke(this, EventArgs.Empty); }
    private void Deselect_Click(object sender, RoutedEventArgs e) => ViewModel.DeselectAll();
}
