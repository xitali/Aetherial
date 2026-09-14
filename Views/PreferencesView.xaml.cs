using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using DiskOptimizer.Models;
using DiskOptimizer.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DiskOptimizer.Views;

public partial class PreferencesView : UserControl
{
    private readonly SettingsService _settingsService = App.Services.GetRequiredService<SettingsService>();
    public event EventHandler? SettingsApplied;

    public PreferencesView()
    {
        InitializeComponent();
        ReloadSettings();
        Loaded += (_, _) => ReloadSettings();
    }

    public void ReloadSettings()
    {
        var settings = _settingsService.Current;
        ThemePicker.SelectedValue = settings.Theme;
        RefreshMetrics.IsChecked = settings.AutoRefreshMetrics;
        RefreshSeconds.Text = settings.MetricsRefreshSeconds.ToString();
        HardwareScan.IsChecked = settings.AutoScanHardwareOnOpen;
        CleanerScan.IsChecked = settings.AutoScanCleanerOnOpen;
        LargeFilesMb.Text = settings.LargeFileThresholdMb.ToString();
        if (_settingsService.LastError is { } error) ShowStatus(error, true);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!int.TryParse(RefreshSeconds.Text, out int seconds)) throw new ArgumentException("Wpisz liczbę całkowitą sekund od 5 do 120.");
            if (!int.TryParse(LargeFilesMb.Text, out int megabytes)) throw new ArgumentException("Wpisz liczbę całkowitą MB od 100 do 10 240.");
            // Clone the most recent settings so a validation or write failure cannot change the live state.
            var settings = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(_settingsService.Current))!;
            settings.Theme = ThemePicker.SelectedValue as string ?? "Dark";
            settings.AutoRefreshMetrics = RefreshMetrics.IsChecked == true;
            settings.MetricsRefreshSeconds = seconds;
            settings.AutoScanHardwareOnOpen = HardwareScan.IsChecked == true;
            settings.AutoScanCleanerOnOpen = CleanerScan.IsChecked == true;
            settings.LargeFileThresholdMb = megabytes;
            settings.Validate();
            _settingsService.SaveSettings(settings);
            SettingsApplied?.Invoke(this, EventArgs.Empty);
            ShowStatus("Zapisano i zastosowano preferencje.", false);
        }
        catch (Exception exception)
        {
            ShowStatus(exception.Message, true);
        }
    }

    private void ShowStatus(string message, bool error)
    {
        SaveStatus.Text = message;
        SaveStatus.SetResourceReference(TextBlock.ForegroundProperty, error ? "ErrorBrush" : "SuccessBrush");
    }
}
