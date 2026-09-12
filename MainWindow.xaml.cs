using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DiskOptimizer.Models;
using DiskOptimizer.Services;
using Microsoft.Win32;
using System.Linq;

namespace DiskOptimizer;

public partial class MainWindow : Window
{
    private readonly DiskScannerService _scanner = new();
    private readonly DiskCleanerService _cleaner = new();
    private readonly FileSystemExplorerService _explorerService = new();
    private readonly MemoryOptimizerService _memoryService = new();
    private readonly SymlinkService _symlinkService = new();
    private readonly DevProjectsService _devProjectsService = new();
    private readonly CompactOsService _compactService = new();
    private readonly DriverUpdaterService _driverService = new();
    private readonly SoftwareInstallerService _appInstaller = new();
    private readonly SettingsService _settingsService = new();

    public ObservableCollection<DriveModel> Drives { get; } = new();
    public ObservableCollection<CleanItem> CleanItems { get; } = new();
    public ObservableCollection<FileSystemItem> ExplorerItems { get; } = new();
    public ObservableCollection<SymlinkPreset> SymlinkPresets { get; } = new();
    public ObservableCollection<DevArtifactItem> DevArtifactItems { get; } = new();
    public ObservableCollection<DriverUpdateItem> DriverUpdates { get; } = new();
    public ObservableCollection<PnpDeviceItem> PnpDevices { get; } = new();
    public ObservableCollection<DiagnosticItem> DiagnosticItems { get; } = new();
    public ObservableCollection<AppPackageItem> AppsList { get; } = new();

    private List<AppPackageItem> _allApps = new();
    private string _selectedCategory = "All";
    private static string SystemDrive => Path.GetPathRoot(Environment.SystemDirectory)!;
    private string _currentExplorerPath = SystemDrive;
    private bool _isBusy = false;
    private DispatcherTimer? _optimizedTimer;
    private int _optimizedCountdown = 30;
    private bool _settingsUiReady;
    private int _explorerRequest;
    private bool _hasCompletedScan;
    private bool _hardwareRefreshInProgress;
    private bool _diagnosticsRefreshInProgress;
    private readonly DispatcherTimer _metricsTimer = new() { Interval = TimeSpan.FromSeconds(15) };

    public MainWindow()
    {
        InitializeComponent();
        App.ApplyTheme(_settingsService.Current.Theme == "Light");

        DrivesItemsControl.ItemsSource = Drives;
        CleanItemsControl.ItemsSource = CleanItems;
        ExplorerItemsControl.ItemsSource = ExplorerItems;
        SymlinkPresetsControl.ItemsSource = SymlinkPresets;
        DevArtifactsControl.ItemsSource = DevArtifactItems;
        DriverUpdatesControl.ItemsSource = DriverUpdates;
        PnpDevicesControl.ItemsSource = PnpDevices;
        DiagnosticItemsControl.ItemsSource = DiagnosticItems;
        AppsItemsControl.ItemsSource = AppsList;

        // Inicjalizacja ustawień w UI
        SettingsInstallPathTextBox.Text = _settingsService.Current.DefaultInstallFolder;
        AppsTargetFolderText.Text = _settingsService.Current.DefaultInstallFolder;
        OptSilentInstallCheck.IsChecked = _settingsService.Current.SilentInstall;
        OptAutoCheckUpdatesCheck.IsChecked = _settingsService.Current.AutoCheckUpdates;
        OptRecycleBinCheck.IsChecked = _settingsService.Current.RecycleBinDefault;
        RecycleBinCheckBox.IsChecked = _settingsService.Current.RecycleBinDefault;
        _settingsUiReady = true;

        Loaded += MainWindow_Loaded;
        StateChanged += (s, e) =>
        {
            if (MaximizeIconText != null)
                MaximizeIconText.Text = WindowState == WindowState.Maximized ? "❐" : "🗖";
        };
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            RefreshDrives();
            InitializeDefaultItems();
            InitializeAppsCatalog();
            UpdateSystemToggleStates();
            RefreshLiveMetrics();
            _metricsTimer.Tick += (_, _) => RefreshLiveMetrics();
            _metricsTimer.Start();
            Closed += (_, _) => { _metricsTimer.Stop(); _optimizedTimer?.Stop(); _modalTcs?.TrySetResult(false); };
            AppendLog($"Aetherial Suite {typeof(App).Assembly.GetName().Version} • Administrator: {(DiskHelper.IsAdministrator() ? "tak" : "nie")}");
            await LoadSymlinkPresetsAsync();
            await NavigateToFolderAsync(SystemDrive);
            await Task.WhenAll(ScanAllItemsAsync(), RefreshDiagnosticsAsync(), RefreshHardwareInventoryAsync(), RefreshDashboardHardwareAsync());
            if (_settingsService.Current.AutoCheckUpdates)
            {
                await _appInstaller.RefreshInstalledStatusesAsync(_allApps, _settingsService.Current.DefaultInstallFolder, AppendLog);
                ApplyAppsFilter();
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Błąd inicjalizacji: {ex.Message}");
            GlobalStatusText.Text = "Nie ukończono wszystkich odczytów. Spróbuj odświeżyć dane.";
        }
    }

    private void RefreshLiveMetrics()
    {
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        TopUptimeText.Text = $"{uptime.Days} d {uptime.Hours:D2} h {uptime.Minutes:D2} min";
        UpdateRamMetricsDisplay();
        if (!_isBusy) RefreshDrives();
    }

    private async Task RefreshHardwareInventoryAsync()
    {
        if (_hardwareRefreshInProgress) return;
        _hardwareRefreshInProgress = true;
        try
        {
            var devices = await _driverService.GetConnectedPnpDevicesAsync(AppendLog);
            PnpDevices.Clear();
            foreach (var device in devices) PnpDevices.Add(device);
            HardwareInventorySummaryText.Text = $"{devices.Count} urządzeń • odczyt {DateTime.Now:HH:mm}";
            var updates = await _driverService.SearchDriverUpdatesAsync(AppendLog);
            DriverUpdates.Clear();
            foreach (var update in updates) DriverUpdates.Add(update);
        }
        catch (Exception ex) { HardwareInventorySummaryText.Text = "Odczyt niedostępny — spróbuj ponownie"; AppendLog($"Nie ukończono odczytu urządzeń: {ex.Message}"); }
        finally { _hardwareRefreshInProgress = false; }
    }

    private async Task RefreshDashboardHardwareAsync()
    {
        var (ok, output, _) = await DiskHelper.RunPowerShellScriptAsync("$ErrorActionPreference='Stop'; $cpu=Get-CimInstance Win32_Processor; $gpu=Get-CimInstance Win32_VideoController; $net=Get-NetAdapter | Where-Object Status -eq 'Up'; [pscustomobject]@{Cpu=($cpu.Name -join ', ');Gpu=($gpu.Name -join ', ');Network=($net.Name -join ', ')} | ConvertTo-Json -Compress", AppendLog);
        if (!ok)
        {
            DashboardCpuText.Text = DashboardGpuText.Text = DashboardNetworkText.Text = "Brak odczytu — spróbuj odświeżyć";
            return;
        }
        try
        {
            using var data = System.Text.Json.JsonDocument.Parse(output);
            DashboardCpuText.Text = data.RootElement.GetProperty("Cpu").GetString() ?? "Brak odczytu";
            DashboardGpuText.Text = data.RootElement.GetProperty("Gpu").GetString() ?? "Brak odczytu";
            var network = data.RootElement.GetProperty("Network").GetString();
            DashboardNetworkText.Text = string.IsNullOrWhiteSpace(network) ? "Brak aktywnych kart" : network;
        }
        catch (Exception ex) { AppendLog($"Nieprawidłowy odczyt metryk: {ex.Message}"); }
        DashboardSecurityText.Text = "Stan ochrony: sprawdź Zabezpieczenia Windows";
    }

    // ==================== PASEK TYTUŁOWY (CUSTOM TITLE BAR) ====================

    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        var settings = _settingsService.Current;
        settings.Theme = settings.Theme == "Light" ? "Dark" : "Light";
        App.ApplyTheme(settings.Theme == "Light");
        _settingsService.SaveSettings(settings);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            MaximizeIconText.Text = "🗖";
        }
        else
        {
            WindowState = WindowState.Maximized;
            MaximizeIconText.Text = "❐";
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    // ==================== NOWOCZESNE OKNA MODALNE (MODAL DIALOGS) ====================

    private TaskCompletionSource<bool>? _modalTcs;

    public Task<bool> ShowConfirmAsync(string title, string message, string confirmText = "Tak, wykonaj", string cancelText = "Anuluj", bool isDanger = false, string icon = "❓")
    {
        _modalTcs?.TrySetResult(false);
        _modalTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ModalIconText.Text = icon;
        ModalTitleText.Text = title;
        ModalMessageText.Text = message;
        ModalCategoryText.Text = isDanger ? "Wymagana ostrożność" : "Potwierdzenie operacji";
        ModalConfirmButton.Content = confirmText;
        ModalConfirmButton.Style = (Style)FindResource(isDanger ? "DangerButtonStyle" : "PrimaryButtonStyle");
        ModalCancelButton.Content = cancelText;
        ModalCancelButton.Visibility = Visibility.Visible;
        ModalOverlay.Visibility = Visibility.Visible;
        return _modalTcs.Task;
    }

    public Task ShowAlertAsync(string title, string message, string buttonText = "Rozumiem", string icon = "ℹ️", bool isSuccess = false)
    {
        _modalTcs?.TrySetResult(false);
        _modalTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ModalIconText.Text = icon;
        ModalTitleText.Text = title;
        ModalMessageText.Text = message;
        ModalCategoryText.Text = isSuccess ? "Operacja ukończona" : "Informacja systemowa";
        ModalConfirmButton.Content = buttonText;
        ModalConfirmButton.Style = (Style)FindResource("PrimaryButtonStyle");
        ModalCancelButton.Visibility = Visibility.Collapsed;
        ModalOverlay.Visibility = Visibility.Visible;
        return _modalTcs.Task;
    }

    private void ModalConfirm_Click(object sender, RoutedEventArgs e)
    {
        ModalOverlay.Visibility = Visibility.Collapsed;
        _modalTcs?.TrySetResult(true);
    }

    private void ModalCancel_Click(object sender, RoutedEventArgs e)
    {
        ModalOverlay.Visibility = Visibility.Collapsed;
        _modalTcs?.TrySetResult(false);
    }

    private void ModalBackdrop_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (ModalCancelButton.Visibility == Visibility.Collapsed)
        {
            ModalOverlay.Visibility = Visibility.Collapsed;
            _modalTcs?.TrySetResult(true);
        }
    }

    // ==================== NAWIGACJA BOCZNA (SIDEBAR) ====================

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && int.TryParse(rb.Tag?.ToString(), out int viewIndex))
        {
            SwitchView(viewIndex);
        }
    }

    private void SwitchView(int viewIndex)
    {
        ViewDashboard.Visibility = viewIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        ViewCleaner.Visibility = viewIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        ViewExplorer.Visibility = viewIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        ViewDrivers.Visibility = viewIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
        ViewTools.Visibility = viewIndex == 4 ? Visibility.Visible : Visibility.Collapsed;
        ViewApps.Visibility = viewIndex == 5 ? Visibility.Visible : Visibility.Collapsed;
        ViewSettings.Visibility = viewIndex == 6 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void NavToCleaner_Click(object sender, RoutedEventArgs e)
    {
        NavCleanerRadio.IsChecked = true;
        SwitchView(1);
    }

    private void NavToExplorer_Click(object sender, RoutedEventArgs e)
    {
        NavExplorerRadio.IsChecked = true;
        SwitchView(2);
    }

    private void NavToDrivers_Click(object sender, RoutedEventArgs e)
    {
        NavDriversRadio.IsChecked = true;
        SwitchView(3);
    }

    private void NavToTools_Click(object sender, RoutedEventArgs e)
    {
        NavToolsRadio.IsChecked = true;
        SwitchView(4);
    }

    private void NavToApps_Click(object sender, RoutedEventArgs e)
    {
        NavAppsRadio.IsChecked = true;
        SwitchView(5);
    }

    private void NavToSettings_Click(object sender, RoutedEventArgs e)
    {
        NavSettingsRadio.IsChecked = true;
        SwitchView(6);
    }

    // ==================== DYSKI & METRYKI ====================

    private void RefreshDrives()
    {
        Drives.Clear();
        var detected = _scanner.GetDrives();
        long totalFree = 0;
        foreach (var d in detected)
        {
            Drives.Add(d);
            totalFree += d.FreeBytes;
        }
        DrivesSummaryText.Text = $"Łącznie wolne na wszystkich dyskach: {DriveModel.FormatBytes(totalFree)}";
    }

    private void UpdateRamMetricsDisplay()
    {
        var (total, avail, load) = MemoryOptimizerService.GetMemoryMetrics();
        long usedBytes = (long)(total - avail);
        RamStatusText.Text = $"Zużycie RAM: {load}% • Używane: {DriveModel.FormatBytes(usedBytes)} z {DriveModel.FormatBytes((long)total)} (Dostępne: {DriveModel.FormatBytes((long)avail)})";
    }

    private void InitializeDefaultItems()
    {
        CleanItems.Clear();
        var defaults = _scanner.GetDefaultItems();
        foreach (var item in defaults)
        {
            item.PropertyChanged += Item_PropertyChanged;
            CleanItems.Add(item);
        }
        UpdateSummaries();
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CleanItem.IsSelected) || e.PropertyName == nameof(CleanItem.SizeBytes))
        {
            UpdateSummaries();
        }
    }

    private void ExplorerItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FileSystemItem.IsSelected))
        {
            UpdateSummaries();
        }
    }

    private void DevArtifact_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DevArtifactItem.IsSelected))
        {
            UpdateSummaries();
        }
    }

    private void UpdateSummaries()
    {
        if (Dispatcher.CheckAccess())
        {
            ApplySummaries();
        }
        else
        {
            Dispatcher.Invoke(ApplySummaries);
        }
    }

    private void ApplySummaries()
    {
        var selectedClean = CleanItems.Where(i => i.IsSelected).ToList();
        long autoBytes = selectedClean.Sum(i => i.SizeBytes);
        int autoCount = selectedClean.Count;
        long totalCleanable = CleanItems.Sum(i => i.SizeBytes);

        long explorerBytes = ExplorerItems.Where(f => f.IsSelected).Sum(f => f.SizeBytes);
        int explorerSelectedCount = ExplorerItems.Count(f => f.IsSelected);

        if (AutoCleanSummaryText != null)
            AutoCleanSummaryText.Text = $"Zaznaczono: {DriveModel.FormatBytes(autoBytes)} ({autoCount} el.)";

        if (ExplorerSelectedSummaryText != null)
            ExplorerSelectedSummaryText.Text = $"Zaznaczono: {DriveModel.FormatBytes(explorerBytes)} ({explorerSelectedCount} el.)";

        // Obliczanie rozmiaru shaderów GPU z CleanItems lub bezpośrednio z dysku
        var shaderItem = CleanItems.FirstOrDefault(i => i.Id == "nvidia_dxcache");
        long shaderBytes = shaderItem != null ? shaderItem.SizeBytes : 0;

        if (DashShadersText != null)
        {
            if (shaderBytes > 0)
            {
                DashShadersText.Text = $"{DriveModel.FormatBytes(shaderBytes)} (Gotowe do usunięcia)";
                DashShadersText.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            }
            else
            {
                DashShadersText.Text = _hasCompletedScan ? "0 B w sprawdzonych lokalizacjach" : "Oczekiwanie na skan";
                DashShadersText.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            }
        }

        if (DashCacheText != null)
        {
            if (totalCleanable > 0)
            {
                DashCacheText.Text = DriveModel.FormatBytes(totalCleanable);
                DashCacheText.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            }
            else
            {
                DashCacheText.Text = _hasCompletedScan ? "0 B w sprawdzonych lokalizacjach" : "Oczekiwanie na skan";
                DashCacheText.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            }
        }

        // Dynamiczna aktualizacja wyglądu karty 1-Klik
        bool isOpt = _hasCompletedScan && totalCleanable == 0;
        if (Dash1ClickCardBorder != null)
        {
            Dash1ClickCardBorder.BorderBrush = (Brush)(new BrushConverter().ConvertFrom(isOpt ? "#10B981" : "#0078D4") ?? Brushes.Blue);
            Dash1ClickCardBorder.SetResourceReference(Border.BackgroundProperty, "CardBgBrush");
        }
        if (Dash1ClickBadge != null)
        {
            Dash1ClickBadge.Background = (Brush)(new BrushConverter().ConvertFrom(isOpt ? "#10B981" : "#1976D2") ?? Brushes.Blue);
        }
        if (Dash1ClickBadgeText != null)
        {
            Dash1ClickBadgeText.Text = isOpt ? "BRAK WYKRYTEGO CACHE" : "SPRAWDŹ ZAKRES";
        }
        if (Dash1ClickButton != null)
        {
            Dash1ClickButton.Content = isOpt ? "Skanuj ponownie" : "Wyczyść wybrane elementy";
        }
    }

    // ==================== WIDOK 2: EKSPLORATOR PARTYCJII ====================

    private async Task NavigateToFolderAsync(string folderPath)
    {
        if (!Directory.Exists(folderPath)) { GlobalStatusText.Text = "Folder nie istnieje lub jest niedostępny."; return; }
        int request = ++_explorerRequest;

        _currentExplorerPath = folderPath;
        CurrentPathTextBox.Text = folderPath;
        GlobalStatusText.Text = $"⏳ Odczytywanie folderu: {folderPath}...";

        ExplorerItems.Clear();
        var items = await _explorerService.GetFolderContentsAsync(folderPath);
        if (request != _explorerRequest) return;
        ExplorerItems.Clear();

        foreach (var item in items)
        {
            item.PropertyChanged += ExplorerItem_PropertyChanged;
            ExplorerItems.Add(item);
        }

        UpdateSummaries();
        GlobalStatusText.Text = $"Załadowano {ExplorerItems.Count} elementów z {folderPath}.";
    }

    private async void SwitchPartition_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string driveRoot)
        {
            await NavigateToFolderAsync(driveRoot);
        }
    }

    private async void NavigateUp_Click(object sender, RoutedEventArgs e)
    {
        var parent = Directory.GetParent(_currentExplorerPath);
        if (parent != null && Directory.Exists(parent.FullName))
        {
            await NavigateToFolderAsync(parent.FullName);
        }
        else
        {
            string root = Path.GetPathRoot(_currentExplorerPath) ?? SystemDrive;
            await NavigateToFolderAsync(root);
        }
    }

    private async void NavigatePath_Click(object sender, RoutedEventArgs e)
    {
        string path = CurrentPathTextBox.Text.Trim();
        if (!string.IsNullOrEmpty(path))
        {
            await NavigateToFolderAsync(path);
        }
    }

    private async void CurrentPathTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            string path = CurrentPathTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(path))
            {
                await NavigateToFolderAsync(path);
            }
        }
    }

    private async void EnterFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path && Directory.Exists(path))
        {
            await NavigateToFolderAsync(path);
        }
    }

    private async void FindLargeFiles_Click(object sender, RoutedEventArgs e)
    {
        int request = ++_explorerRequest;
        string root = Path.GetPathRoot(_currentExplorerPath) ?? SystemDrive;
        GlobalStatusText.Text = $"⏳ Szukanie plików > 500 MB na partycji {root}...";
        AppendLog($"Rozpoczęto skanowanie partycji {root} pod kątem plików > 500 MB...");

        ExplorerItems.Clear();
        var largeFiles = await _explorerService.FindLargeFilesAsync(root, 500 * 1024 * 1024);
        if (request != _explorerRequest) return;
        ExplorerItems.Clear();

        foreach (var item in largeFiles)
        {
            item.PropertyChanged += ExplorerItem_PropertyChanged;
            ExplorerItems.Add(item);
        }

        UpdateSummaries();
        GlobalStatusText.Text = $"Wykryto {largeFiles.Count} plików > 500 MB na partycji {root}.";
        AppendLog($"✓ Skaner Gigantów: Znaleziono {largeFiles.Count} plików na partycji {root}.");
    }

    private void SelectByAge_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int days))
        {
            var threshold = DateTime.Now.AddDays(-days);
            foreach (var item in ExplorerItems)
            {
                item.IsSelected = (days == 0) || (item.LastModified <= threshold);
            }
            UpdateSummaries();
        }
    }

    private void DeselectExplorer_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in ExplorerItems) item.IsSelected = false;
        UpdateSummaries();
    }

    // ==================== WIDOK 3: MIGRACJA & SYMLINKI ====================

    private async Task LoadSymlinkPresetsAsync()
    {
        SymlinkPresets.Clear();
        var presets = _symlinkService.GetDefaultPresets();
        foreach (var p in presets)
        {
            await _symlinkService.RefreshPresetStatusAsync(p);
            SymlinkPresets.Add(p);
        }
    }

    private async void ExecutePresetRelocation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is SymlinkPreset preset)
        {
            if (preset.IsJunction)
            {
                await ShowAlertAsync("Folder zmigrowany", "Ten folder został już zmigrowany i posiada aktywne dowiązanie NTFS.", "Rozumiem", "ℹ️");
                return;
            }

            var confirm = await ShowConfirmAsync(
                "Potwierdzenie migracji danych",
                $"Czy chcesz przenieść dane z:\n{preset.SourcePath}\n\nDo nowej lokalizacji:\n{preset.SuggestedDestPath}\n\nI utworzyć przezroczyste dowiązanie NTFS Junction (mklink /J)?",
                "Tak, przenieś dane",
                "Anuluj",
                isDanger: false,
                icon: "🔗");

            if (!confirm) return;

            LogDrawerBorder.Visibility = Visibility.Visible;
            AppendLog($"Rozpoczynam migrację presetu: {preset.Title}...");

            bool ok = await _symlinkService.RelocateAndCreateJunctionAsync(
                preset.SourcePath,
                preset.SuggestedDestPath,
                AppendLog);

            if (ok)
            {
                await _symlinkService.RefreshPresetStatusAsync(preset);
                SymlinkPresetsControl.Items.Refresh();
                RefreshDrives();
                await ShowAlertAsync("Migracja ukończona", "Utworzono dowiązanie do nowej lokalizacji. Kopia źródłowa .aetherial-backup pozostaje na dysku. Sprawdź działanie aplikacji przed jej ręcznym usunięciem.", "Świetnie", "🎉", isSuccess: true);
            }
        }
    }

    // ==================== WIDOK 4: DEWELOPER & AI ====================

    private async void ScanDevArtifacts_Click(object sender, RoutedEventArgs e)
    {
        string root = DevRootPathTextBox.Text.Trim();
        if (!Directory.Exists(root))
        {
            await ShowAlertAsync("Błąd ścieżki", $"Folder projektów nie istnieje: {root}", "Rozumiem", "⚠️");
            return;
        }

        int minDays = 0;
        if (DevInactiveComboBox.SelectedItem is ComboBoxItem cbi && int.TryParse(cbi.Tag?.ToString(), out int days))
        {
            minDays = days;
        }

        GlobalStatusText.Text = $"⏳ Skanowanie projektów w {root}...";
        AppendLog($"Skanowanie projektów deweloperskich w {root} (filtr nieaktywności: > {minDays} dni)...");

        DevArtifactItems.Clear();
        var items = await _devProjectsService.ScanDevProjectsAsync(root, minDays);

        foreach (var item in items)
        {
            item.PropertyChanged += DevArtifact_PropertyChanged;
            DevArtifactItems.Add(item);
        }

        UpdateSummaries();
        long totalBytes = DevArtifactItems.Sum(i => i.SizeBytes);
        GlobalStatusText.Text = $"Znaleziono {DevArtifactItems.Count} folderów pakietów ({DriveModel.FormatBytes(totalBytes)}).";
        AppendLog($"✓ Znaleziono {DevArtifactItems.Count} folderów buildów/pakietów ({DriveModel.FormatBytes(totalBytes)} do odzyskania).");
    }

    private async void CleanDevArtifacts_Click(object sender, RoutedEventArgs e)
    {
        var selected = DevArtifactItems.Where(i => i.IsSelected).ToList();
        if (!selected.Any())
        {
            await ShowAlertAsync("Brak zaznaczenia", "Nie zaznaczono żadnych pakietów do usunięcia.", "Rozumiem", "ℹ️");
            return;
        }

        long size = selected.Sum(i => i.SizeBytes);
        var confirm = await ShowConfirmAsync(
            "Potwierdzenie czyszczenia deweloperskiego",
            $"Czy na pewno chcesz usunąć {selected.Count} folderów pakietów (node_modules, .venv, bin/obj)?\n\nZwolnione miejsce: {DriveModel.FormatBytes(size)}\n\nSprawdź listę folderów. Operacja usuwa ich całą zawartość; odtworzenie pakietów wymaga menedżera zależności.",
            "Tak, usuń pakiety",
            "Anuluj",
            isDanger: true,
            icon: "🧹");

        if (!confirm) return;

        LogDrawerBorder.Visibility = Visibility.Visible;
        var (freed, count) = await _devProjectsService.CleanArtifactsAsync(selected, AppendLog);

        RefreshDrives();
        UpdateSummaries();

        // Usuń wyczyszczone z listy
        foreach (var item in selected.Where(i => !Directory.Exists(i.FullPath))) DevArtifactItems.Remove(item);

        await ShowAlertAsync("Wynik czyszczenia", $"Usunięto {count} folderów. Zwolniono: {DriveModel.FormatBytes(freed)}. Pozostałe lub niedostępne foldery pozostają na liście.", "Zamknij", "ℹ️", isSuccess: count == selected.Count);
    }

    // ==================== WIDOK 5: WYDAJNOŚĆ & RAM ====================

    private async void OptimizeRam_Click(object sender, RoutedEventArgs e)
    {
        LogDrawerBorder.Visibility = Visibility.Visible;
        GlobalStatusText.Text = "⏳ Trwa optymalizacja pamięci RAM...";
        var (freed, count) = await _memoryService.OptimizeRamAsync(AppendLog);
        UpdateRamMetricsDisplay();
        GlobalStatusText.Text = $"Pamięć RAM zoptymalizowana! Uwolniono {DriveModel.FormatBytes(freed)}.";
    }

    private async void DisableHibernation_Click(object sender, RoutedEventArgs e)
    {
        LogDrawerBorder.Visibility = Visibility.Visible;
        await _cleaner.SetHibernationModeAsync("off", AppendLog);
        RefreshDrives();
        UpdateSystemToggleStates();
    }

    private async void ReduceHibernation_Click(object sender, RoutedEventArgs e)
    {
        LogDrawerBorder.Visibility = Visibility.Visible;
        await _cleaner.SetHibernationModeAsync("reduced", AppendLog);
        RefreshDrives();
        UpdateSystemToggleStates();
    }

    private async void EnableHibernation_Click(object sender, RoutedEventArgs e)
    {
        LogDrawerBorder.Visibility = Visibility.Visible;
        await _cleaner.SetHibernationModeAsync("full", AppendLog);
        RefreshDrives();
        UpdateSystemToggleStates();
    }

    private async void ShrinkDockerVhdx_Click(object sender, RoutedEventArgs e)
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string vhdx = Path.Combine(localAppData, @"Docker\wsl\disk\docker_data.vhdx");

        if (!File.Exists(vhdx))
        {
            await ShowAlertAsync("Brak pliku Docker", $"Nie odnaleziono pliku wirtualnego dysku Docker:\n{vhdx}", "Rozumiem", "ℹ️");
            return;
        }

        LogDrawerBorder.Visibility = Visibility.Visible;
        await _compactService.ShrinkVhdxAsync(vhdx, AppendLog);
        RefreshDrives();
    }

    private async void CompactFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Wybierz folder do bezstratnej kompresji NTFS CompactOS"
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.FolderName))
        {
            LogDrawerBorder.Visibility = Visibility.Visible;
            await _compactService.CompressFolderAsync(dialog.FolderName, AppendLog);
            RefreshDrives();
        }
    }

    // ==================== GŁÓWNE SKANOWANIE I CZYSZCZENIE ====================

    private async Task ScanAllItemsAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        if (ScanAllButton != null) ScanAllButton.IsEnabled = false;
        if (MasterScanButton != null) MasterScanButton.IsEnabled = false;
        GlobalStatusText.Text = "⏳ Trwa pełne skanowanie systemu (dyski, RAM i śmieci)...";
        AppendLog("=== Rozpoczęto pełne skanowanie dysków i pamięci podręcznych ===");

        RefreshDrives();
        UpdateRamMetricsDisplay();

        try
        {
            foreach (var item in CleanItems)
            {
                GlobalStatusText.Text = $"⏳ Skanowanie: {item.Title}...";
                await _scanner.ScanItemAsync(item);
                if (item.SizeBytes > 0)
                {
                    AppendLog($"[Wykryto] {item.Title}: {item.FormattedSize} ({item.ItemCount} el.)");
                }
            }

            _hasCompletedScan = true;
            UpdateSummaries();
            long total = CleanItems.Where(i => i.IsSelected && i.CanClean).Sum(i => i.SizeBytes);
            GlobalStatusText.Text = $"Gotowe. Wykryto {DriveModel.FormatBytes(total)} w pamięci podręcznej i shaderach.";
            AppendLog($"=== Skanowanie zakończone. Łącznie w cache: {DriveModel.FormatBytes(total)} ===");
        }
        finally
        {
            _isBusy = false;
            if (ScanAllButton != null) ScanAllButton.IsEnabled = true;
            if (MasterScanButton != null) MasterScanButton.IsEnabled = true;
        }
    }

    private async void MasterScanAll_Click(object sender, RoutedEventArgs e)
    {
        await ScanAllItemsAsync();
    }

    private async void ScanAllButton_Click(object sender, RoutedEventArgs e)
    {
        await ScanAllItemsAsync();
    }

    // ==================== 1-KLIK: INTELIGENTNA AUTOMATYCZNA OPTYMALIZACJA ====================

    private async void AutoOptimize1Click_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;

        if (!_hasCompletedScan) { await ScanAllItemsAsync(); return; }
        var cleanableItems = CleanItems.Where(i => i.IsSelected && i.CanClean && i.SizeBytes > 0).ToList();
        if (cleanableItems.Count == 0) { await ScanAllItemsAsync(); return; }
        long cacheBytes = cleanableItems.Sum(i => i.SizeBytes);

        var confirm = await ShowConfirmAsync(
            "Inteligentna automatyczna optymalizacja 1-kliknięciem",
            $"Operacja usunie zaznaczone kategorie: {cleanableItems.Count}, szacunkowo {DriveModel.FormatBytes(cacheBytes)}.\n\n" +
            string.Join("\n", cleanableItems.Select(i => "• " + i.Title)) +
            "\n\nSprawdź zaznaczenie, szczególnie Kosz i Pobrane. Usuniętych plików może nie dać się odzyskać. Pamięć podręczna zostanie odtworzona przez aplikacje.",
            "⚡ Optymalizuj system teraz",
            "Anuluj",
            isDanger: true,
            icon: "⚡");

        if (!confirm) return;

        _isBusy = true;
        ScanAllButton.IsEnabled = false;
        LogDrawerBorder.Visibility = Visibility.Visible;
        ToggleLogButton.Content = "📋 Ukryj dziennik zdarzeń";

        AppendLog("===============================================================");
        AppendLog("⚡ ROZPOCZĘTO INTELIGENTNĄ AUTOMATYCZNĄ OPTYMALIZACJĘ 1-KLIK ⚡");
        AppendLog("===============================================================");

        long grandFreed = 0;
        int grandFiles = 0;

        try
        {
            // 1. Czyść wszystkie bezpieczne elementy cache
            foreach (var item in cleanableItems)
            {
                GlobalStatusText.Text = $"⚡ Automatyczne czyszczenie: {item.Title}...";
                var (freed, files) = await _cleaner.CleanItemAsync(item, AppendLog);
                grandFreed += freed;
                grandFiles += files;
            }

            long ramFreed = 0; // Ta operacja obejmuje wyłącznie zaznaczone pliki.

            // 5. Reskanowanie wyczyszczonych elementów
            foreach (var item in cleanableItems)
            {
                await _scanner.ScanItemAsync(item);
                if (item.SizeBytes == 0) item.IsSelected = false;
            }

            RefreshDrives();
            UpdateRamMetricsDisplay();
            UpdateSummaries();

            AppendLog("===============================================================");
            AppendLog($"✓ AUTOMATYCZNA OPTYMALIZACJA ZAKOŃCZONA SUKCESEM!");
            AppendLog($"✓ Zwolnione miejsce na dyskach: {DriveModel.FormatBytes(grandFreed)} ({grandFiles} plików)");
            AppendLog($"✓ Uwolniona pamięć RAM: {DriveModel.FormatBytes(ramFreed)}");
            AppendLog("Szczegóły pominiętych lub niedostępnych plików znajdują się w dzienniku.");
            AppendLog("===============================================================");

            GlobalStatusText.Text = $"System zoptymalizowany! Zwolniono {DriveModel.FormatBytes(grandFreed)} i {DriveModel.FormatBytes(ramFreed)} RAM.";

            // Aktywacja stanu Zoptymalizowanego oraz pełnoekranowego okna sukcesu (min. 30 sek)
            _hasCompletedScan = true;
            UpdateSummaries();

            if (OverlayFreedDiskText != null)
                OverlayFreedDiskText.Text = DriveModel.FormatBytes(grandFreed);
            if (OverlayFreedRamText != null)
                OverlayFreedRamText.Text = DriveModel.FormatBytes(ramFreed);

            _optimizedCountdown = 30;
            if (OverlayTimerText != null)
                OverlayTimerText.Text = $"Ekran optymalizacji aktywny (pozostało: {_optimizedCountdown}s)...";

            if (FullOptimizedOverlay != null)
                FullOptimizedOverlay.Visibility = Visibility.Visible;

            _optimizedTimer?.Stop();
            _optimizedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _optimizedTimer.Tick += (s, ev) =>
            {
                _optimizedCountdown--;
                if (OverlayTimerText != null)
                    OverlayTimerText.Text = $"Ekran optymalizacji aktywny (pozostało: {_optimizedCountdown}s)...";

                if (_optimizedCountdown <= 0)
                {
                    _optimizedTimer?.Stop();
                    if (FullOptimizedOverlay != null)
                        FullOptimizedOverlay.Visibility = Visibility.Collapsed;
                }
            };
            _optimizedTimer.Start();
        }
        catch (Exception ex)
        {
            AppendLog($"[BŁĄD] Wystąpił wyjątek podczas optymalizacji: {ex.Message}");
            await ShowAlertAsync("Uwaga", $"Wystąpił problem podczas optymalizacji: {ex.Message}", "Rozumiem", "⚠️");
        }
        finally
        {
            _isBusy = false;
            ScanAllButton.IsEnabled = true;
        }
    }

    // ==================== WIDOK 1: CZYSZCZENIE ZAZNACZONYCH CACHE ====================

    private async void CleanSelectedCache_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;

        var selected = CleanItems.Where(i => i.IsSelected && i.CanClean && i.SizeBytes > 0).ToList();
        if (!selected.Any())
        {
            await ShowAlertAsync("Brak zaznaczenia", "Nie zaznaczono żadnych pamięci podręcznych do wyczyszczenia (lub ich rozmiar wynosi 0 B).", "Rozumiem", "ℹ️");
            return;
        }

        long estimatedBytes = selected.Sum(i => i.SizeBytes);

        var confirm = await ShowConfirmAsync(
            "Potwierdzenie czyszczenia pamięci podręcznych",
            $"Czy na pewno chcesz usunąć wybrane pamięci podręczne ({selected.Count} pozycji)?\n\n" +
            $"Szacowane zwolnione miejsce: {DriveModel.FormatBytes(estimatedBytes)}\n" +
            $"W tym shadery GPU, bufory przeglądarek i pliki tymczasowe.",
            "🧹 Rozpocznij czyszczenie",
            "Anuluj",
            isDanger: true,
            icon: "🧹");

        if (!confirm) return;

        _isBusy = true;
        ScanAllButton.IsEnabled = false;
        LogDrawerBorder.Visibility = Visibility.Visible;
        ToggleLogButton.Content = "📋 Ukryj dziennik zdarzeń";

        long grandFreed = 0;
        int grandFiles = 0;

        try
        {
            foreach (var item in selected)
            {
                GlobalStatusText.Text = $"🧹 Czyszczenie: {item.Title}...";
                var (freed, files) = await _cleaner.CleanItemAsync(item, AppendLog);
                grandFreed += freed;
                grandFiles += files;
            }

            foreach (var item in selected)
            {
                await _scanner.ScanItemAsync(item);
                if (item.SizeBytes == 0) item.IsSelected = false;
            }

            RefreshDrives();
            UpdateSummaries();

            GlobalStatusText.Text = $"Gotowe! Pomyślnie zwolniono {DriveModel.FormatBytes(grandFreed)}.";
            await ShowAlertAsync("Pamięci wyczyszczone", $"Czyszczenie zakończone sukcesem!\nZwolniono: {DriveModel.FormatBytes(grandFreed)} ({grandFiles} plików).", "Świetnie", "🎉", isSuccess: true);
        }
        finally
        {
            _isBusy = false;
            ScanAllButton.IsEnabled = true;
        }
    }

    // ==================== WIDOK 2: USUWANIE ZAZNACZONYCH W EKSPLORATORZE ====================

    private async void DeleteSelectedExplorerFiles_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;

        var selected = ExplorerItems.Where(f => f.IsSelected && f.CanDelete).ToList();
        if (!selected.Any())
        {
            await ShowAlertAsync("Brak zaznaczenia", "Nie zaznaczono żadnych plików w Eksploratorze do usunięcia (pliki chronione kłódką są zabezpieczone).", "Rozumiem", "ℹ️");
            return;
        }

        long estimatedBytes = selected.Sum(f => f.SizeBytes);
        bool useRecycleBin = RecycleBinCheckBox.IsChecked ?? true;

        var confirm = await ShowConfirmAsync(
            "Potwierdzenie usunięcia plików z dysku",
            $"Czy na pewno chcesz usunąć {selected.Count} zaznaczonych plików / folderów?\n\n" +
            $"Łączny rozmiar: {DriveModel.FormatBytes(estimatedBytes)}\n" +
            $"Tryb: {(useRecycleBin ? "Przeniesienie do Kosza (bezpieczne)" : "Trwałe usunięcie z dysku (bezpowrotne)")}",
            "🗑️ Usuń zaznaczone pliki",
            "Anuluj",
            isDanger: true,
            icon: "🗑️");

        if (!confirm) return;

        _isBusy = true;
        ScanAllButton.IsEnabled = false;
        LogDrawerBorder.Visibility = Visibility.Visible;
        ToggleLogButton.Content = "📋 Ukryj dziennik zdarzeń";

        try
        {
            GlobalStatusText.Text = "🧹 Usuwanie zaznaczonych elementów z dysku...";
            var (freed, count) = await _explorerService.DeleteItemsAsync(selected, useRecycleBin, AppendLog);

            RefreshDrives();
            await NavigateToFolderAsync(_currentExplorerPath);
            UpdateSummaries();

            GlobalStatusText.Text = $"Gotowe! Usunięto {count} elementów i zwolniono {DriveModel.FormatBytes(freed)}.";
            await ShowAlertAsync("Usuwanie zakończone", $"Operacja zakończona sukcesem!\nUsunięto {count} elementów.\nZwolniono: {DriveModel.FormatBytes(freed)}.", "Świetnie", "🎉", isSuccess: true);
        }
        finally
        {
            _isBusy = false;
            ScanAllButton.IsEnabled = true;
        }
    }

    private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
    {
        bool select = SelectAllCheckBox.IsChecked ?? true;
        foreach (var item in CleanItems.Where(i => i.CanClean)) item.IsSelected = select;
        UpdateSummaries();
    }

    private void DeselectAll_Click(object sender, RoutedEventArgs e)
    {
        SelectAllCheckBox.IsChecked = false;
        foreach (var item in CleanItems) item.IsSelected = false;
        UpdateSummaries();
    }

    private void SelectOnlyCache_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in CleanItems)
        {
            bool isCache = item.Category.Contains("GPU", StringComparison.OrdinalIgnoreCase) ||
                           item.Category.Contains("Cache", StringComparison.OrdinalIgnoreCase) ||
                           item.Category.Contains("Tymczasowe", StringComparison.OrdinalIgnoreCase) ||
                           item.Id.Contains("shader", StringComparison.OrdinalIgnoreCase) ||
                           item.Id.Contains("dxcache", StringComparison.OrdinalIgnoreCase) ||
                           item.Id.Contains("glcache", StringComparison.OrdinalIgnoreCase);
            item.IsSelected = isCache;
        }
        UpdateSummaries();
        AppendLog("⚡ Zaznaczono tylko pamięć podręczną Shaderów GPU i pliki cache.");
    }

    private async void ScanSingleItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CleanItem item)
        {
            btn.IsEnabled = false;
            try { await _scanner.ScanItemAsync(item); UpdateSummaries(); }
            finally { btn.IsEnabled = true; }
        }
    }

    private void OpenExplorerPath_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path && !string.IsNullOrEmpty(path))
        {
            try
            {
                if (File.Exists(path)) Process.Start("explorer.exe", $"/select,\"{path}\"");
                else if (Directory.Exists(path)) Process.Start("explorer.exe", $"\"{path}\"");
            }
            catch { }
        }
    }

    // ==================== WIDOK 6: STEROWNIKI & SPRZĘT ====================

    private async void SearchDriverUpdates_Click(object sender, RoutedEventArgs e)
    {
        if (_hardwareRefreshInProgress) return;
        LogDrawerBorder.Visibility = Visibility.Visible;
        GlobalStatusText.Text = "Wyszukiwanie aktualizacji sterowników...";
        await RefreshHardwareInventoryAsync();
        GlobalStatusText.Text = $"Zakończono odczyt. Widoczne aktualizacje: {DriverUpdates.Count}.";
    }

    private async void InstallDriverUpdates_Click(object sender, RoutedEventArgs e)
    {
        var selected = DriverUpdates.Where(i => i.IsSelected).ToList();
        if (!selected.Any())
        {
            await ShowAlertAsync("Brak zaznaczenia", "Nie zaznaczono żadnych sterowników do instalacji.", "Rozumiem", "ℹ️");
            return;
        }

        var confirm = await ShowConfirmAsync(
            "Potwierdzenie instalacji sterowników",
            $"Czy na pewno chcesz pobrać i zainstalować {selected.Count} certyfikowanych aktualizacji sterowników WHQL?\n\nOperacja może chwilowo zamigać ekranem przy aktualizacji sterownika graficznego NVIDIA.",
            "⬇️ Zainstaluj sterowniki",
            "Anuluj",
            isDanger: false,
            icon: "🔌");

        if (!confirm) return;

        LogDrawerBorder.Visibility = Visibility.Visible;
        GlobalStatusText.Text = "⏳ Pobieranie i instalowanie sterowników...";
        foreach (var u in selected)
        {
            u.Status = "Instalowanie...";
            u.StatusColor = "#FFB74D";
        }

        var (success, failed) = await _driverService.InstallSelectedUpdatesAsync(selected, AppendLog);

        foreach (var u in selected)
        {
            u.Status = "Zainstalowano pomyślnie";
            u.StatusColor = "#81C784";
        }

        GlobalStatusText.Text = $"Instalacja ukończona: {success} zainstalowano, {failed} błędów.";
        await ShowAlertAsync("Wynik instalacji", $"Instalacja sterowników zakończona!\nPomyślnie zainstalowano: {success}\nBłędy: {failed}", "Zamknij", "ℹ️", isSuccess: failed == 0);
    }

    private void OpenNvidiaApp_Click(object sender, RoutedEventArgs e)
    {
        DriverUpdaterService.OpenNvidiaApp();
    }

    private void OpenAsrockSupport_Click(object sender, RoutedEventArgs e)
    {
        DriverUpdaterService.OpenAsrockSupport();
    }

    private void OpenMchoseHub_Click(object sender, RoutedEventArgs e)
    {
        DriverUpdaterService.OpenMchoseHub();
    }

    private void OpenKillerSuite_Click(object sender, RoutedEventArgs e)
    {
        DriverUpdaterService.OpenKillerIntelSuite();
    }

    private void OpenAmdChipset_Click(object sender, RoutedEventArgs e)
    {
        DriverUpdaterService.OpenAmdChipsetDrivers();
    }

    private void OpenDeviceManager_Click(object sender, RoutedEventArgs e)
    {
        DriverUpdaterService.OpenDeviceManager();
    }

    private async void SearchAllDrivers_Click(object sender, RoutedEventArgs e)
    {
        LogDrawerBorder.Visibility = Visibility.Visible;
        GlobalStatusText.Text = "⏳ Głęboki skan urządzeń PnP, Centrum Diagnostycznego & sterowników WHQL...";
        AppendLog("=== Rozpoczęto pełne skanowanie sprzętu PnP, diagnostyki i aktualizacji WHQL ===");

        // 1. Odśwież Centrum Diagnostyczne (Dynamiczny stan sprzętu)
        await RefreshDiagnosticsAsync();

        await Task.WhenAll(RefreshHardwareInventoryAsync(), RefreshDashboardHardwareAsync());

        GlobalStatusText.Text = $"Diagnostyka: {DiagnosticItems.Count} zbadanych | Urządzenia PnP: {PnpDevices.Count} | WHQL: {DriverUpdates.Count}";
        AppendLog($"✓ Gotowe. Centrum Diagnostyczne: {DiagnosticItems.Count} pozycji, {PnpDevices.Count} urządzeń PnP, {DriverUpdates.Count} aktualizacji WHQL.");
    }

    public async Task RefreshDiagnosticsAsync()
    {
        if (_diagnosticsRefreshInProgress) return;
        _diagnosticsRefreshInProgress = true;
        try
        {
            var items = await _driverService.GetDynamicDiagnosticItemsAsync(AppendLog);
            Dispatcher.Invoke(() =>
            {
                DiagnosticItems.Clear();
                foreach (var item in items)
                {
                    DiagnosticItems.Add(item);
                }

                int problemCount = items.Count(i => i.IsProblem);
                int healthyCount = items.Count - problemCount;
                HardwareSummaryText.Text = $"{items.Count} odczytów • {problemCount} ostrzeżeń • {DateTime.Now:HH:mm}";
                DashboardHealthText.Text = items.Count == 0 ? "Brak odczytu" : $"{problemCount} ostrzeżeń / {items.Count} odczytów";

                bool unavailable = items.Count == 0 || items.Any(i => i.Id == "unavailable" || i.BadgeText == "Brak danych");
                if (unavailable)
                {
                    DashboardHealthText.Text = "Diagnostyka niedostępna";
                    DiagnosticsMainBorder.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");
                    DiagnosticsIconText.Text = "—";
                    DiagnosticsHeaderTitle.Text = "NIE UZYSKANO WYNIKU DIAGNOSTYKI";
                    DiagnosticsBadgeText.Text = "BRAK DANYCH";
                    DiagnosticsSubtitleText.Text = "Odczyt nie potwierdza sprawności urządzeń. Sprawdź dziennik i ponów skan.";
                }
                else if (problemCount == 0)
                {
                    DiagnosticsMainBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                    DiagnosticsIconText.Text = "🟢";
                    DiagnosticsHeaderTitle.Text = items.Count == 0 ? "BRAK WYNIKÓW DIAGNOSTYKI" : "NIE WYKRYTO PROBLEMÓW W WYKONANYCH TESTACH";
                    DiagnosticsHeaderTitle.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                    DiagnosticsBadgeBorder.SetResourceReference(Border.BackgroundProperty, "AccentSoftBrush");
                    DiagnosticsBadgeText.Text = $"{items.Count} ODCZYTÓW • {problemCount} OSTRZEŻEŃ";
                    DiagnosticsBadgeText.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                    DiagnosticsSubtitleText.Text = "Wynik dotyczy wyłącznie poniższych odczytów. Brak ostrzeżeń nie oznacza pełnego testu sprawności sprzętu.";
                }
                else
                {
                    DiagnosticsMainBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
                    DiagnosticsIconText.Text = "⚠️";
                    DiagnosticsHeaderTitle.Text = "CENTRUM DIAGNOSTYCZNE: STAN HARDWARE & ZALECENIA NAPRAWCZE";
                    DiagnosticsHeaderTitle.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                    DiagnosticsBadgeBorder.SetResourceReference(Border.BackgroundProperty, "AccentSoftBrush");
                    DiagnosticsBadgeText.Text = $"{problemCount} WYMAGA NAPRAWY | {healthyCount} W NORMIE";
                    DiagnosticsBadgeText.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                    DiagnosticsSubtitleText.Text = "Poniżej znajdują się wyniki odczytów Windows oraz zalecenia dla wykrytych urządzeń.";
                }
            });
        }
        catch (Exception ex)
        {
            DashboardHealthText.Text = "Odczyt niedostępny";
            AppendLog($"Błąd odświeżania Centrum Diagnostycznego: {ex.Message}");
        }
        finally { _diagnosticsRefreshInProgress = false; }
    }

    private void DiagnosticAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string target && !string.IsNullOrEmpty(target))
        {
            switch (target.ToLowerInvariant())
            {
                case "install_mediatek":
                    InstallMediaTekDrivers_Click(sender, e);
                    break;
                case "asrock_bios":
                    OpenAsrockBios_Click(sender, e);
                    break;
                case "retrim":
                    RunTrimNow_Click(sender, e);
                    break;
                case "amd":
                    DriverUpdaterService.OpenAmdChipsetDrivers();
                    break;
                case "killer":
                    DriverUpdaterService.OpenKillerIntelSuite();
                    break;
                case "nvidia":
                    DriverUpdaterService.OpenNvidiaApp();
                    break;
                case "mchose":
                    DriverUpdaterService.OpenMchoseHub();
                    break;
                case "devmgmt":
                    DriverUpdaterService.OpenDeviceManager();
                    break;
                default:
                    if (target.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        try { Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true }); } catch { }
                    }
                    else
                    {
                        DriverUpdaterService.OpenDeviceManager();
                    }
                    break;
            }
        }
    }

    private void PnpDeviceAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string target && !string.IsNullOrEmpty(target))
        {
            switch (target.ToLowerInvariant())
            {
                case "mchose":
                    DriverUpdaterService.OpenMchoseHub();
                    break;
                case "nvidia":
                    DriverUpdaterService.OpenNvidiaApp();
                    break;
                case "killer":
                    DriverUpdaterService.OpenKillerIntelSuite();
                    break;
                case "amd":
                    DriverUpdaterService.OpenAmdChipsetDrivers();
                    break;
                case "install_mediatek":
                    InstallMediaTekDrivers_Click(sender, e);
                    break;
                case "devmgmt":
                    DriverUpdaterService.OpenDeviceManager();
                    break;
                default:
                    if (target.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        try { Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true }); } catch { }
                    }
                    else
                    {
                        DriverUpdaterService.OpenDeviceManager();
                    }
                    break;
            }
        }
    }

    private void ToggleLogButton_Click(object sender, RoutedEventArgs e)
    {
        if (LogDrawerBorder.Visibility == Visibility.Visible)
        {
            LogDrawerBorder.Visibility = Visibility.Collapsed;
            ToggleLogButton.Content = "📋 Pokaż dziennik zdarzeń";
        }
        else
        {
            LogDrawerBorder.Visibility = Visibility.Visible;
            ToggleLogButton.Content = "📋 Ukryj dziennik zdarzeń";
        }
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e) => LogTextBox.Clear();

    private void AppendLog(string message)
    {
        if (Dispatcher.HasShutdownStarted) return;
        Dispatcher.Invoke(() =>
        {
            if (LogTextBox.Text.Length > 100000) LogTextBox.Clear();
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            LogTextBox.AppendText(line + Environment.NewLine);
            LogTextBox.ScrollToEnd();
        });
    }

    // ==================== CENTRUM PROGRAMÓW & AKTUALIZATOR (100 APLIKACJI) ====================

    private void InitializeAppsCatalog()
    {
        _allApps = _appInstaller.GetCuratedCatalog();
        ApplyAppsFilter();
    }

    private void ApplyAppsFilter()
    {
        string search = AppsSearchTextBox?.Text?.Trim().ToLowerInvariant() ?? "";
        var filtered = _allApps.Where(a =>
            (_selectedCategory == "All" || a.Category.Equals(_selectedCategory, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(search) ||
             a.Name.ToLowerInvariant().Contains(search) ||
             a.Description.ToLowerInvariant().Contains(search) ||
             a.Id.ToLowerInvariant().Contains(search))
        ).ToList();

        AppsList.Clear();
        foreach (var app in filtered)
        {
            AppsList.Add(app);
        }
    }

    private void AppsSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyAppsFilter();
    }

    private void FilterCategory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string cat)
        {
            _selectedCategory = cat;
            ApplyAppsFilter();
        }
    }

    private void SelectVisibleApps_Click(object sender, RoutedEventArgs e)
    {
        foreach (var app in AppsList)
        {
            app.IsSelected = true;
        }
    }

    private void DeselectAllApps_Click(object sender, RoutedEventArgs e)
    {
        foreach (var app in _allApps)
        {
            app.IsSelected = false;
        }
    }

    private async void RefreshAppsStatus_Click(object sender, RoutedEventArgs e)
    {
        AppendLog("🔄 Rozpoczynam sprawdzanie zainstalowanych programów i aktualizacji winget...");
        GlobalStatusText.Text = "Sprawdzanie zainstalowanych programów winget...";
        await _appInstaller.RefreshInstalledStatusesAsync(_allApps, _settingsService.Current.DefaultInstallFolder, AppendLog);
        ApplyAppsFilter();
        GlobalStatusText.Text = "Zakończono próbę odczytu programów. Szczegóły w dzienniku.";
        int installed = _allApps.Count(a => a.IsInstalled);
        int updates = _allApps.Count(a => a.HasUpdate);
        await ShowAlertAsync("Centrum Programów", $"Katalog: {_allApps.Count} programów. Dostępne wyniki odczytu:\n• Zainstalowane: {installed}\n• Dostępne aktualizacje: {updates}", "Rozumiem", "✅", isSuccess: true);
    }

    private async void InstallSingleApp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is AppPackageItem app)
        {
            if (app.HasUpdate)
            {
                bool ok = await _appInstaller.UpgradeAppAsync(app, AppendLog);
                ApplyAppsFilter();
                if (ok)
                {
                    await ShowAlertAsync("Aktualizacja ukończona", $"Pomyślnie zaktualizowano program {app.Name}.", "OK", "🎉", isSuccess: true);
                }
            }
            else
            {
                string targetPath = _settingsService.Current.DefaultInstallFolder;
                bool ok = await _appInstaller.InstallAppAsync(app, targetPath, AppendLog);
                ApplyAppsFilter();
                if (ok)
                {
                    string msg = app.Status.Contains("Aktualn", StringComparison.OrdinalIgnoreCase)
                        ? $"Program {app.Name} jest już zainstalowany w najnowszej wersji na Twoim komputerze."
                        : $"Pomyślnie zainstalowano {app.Name} w katalogu:\n{targetPath}";
                    await ShowAlertAsync("Centrum Programów", msg, "Świetnie", "🎉", isSuccess: true);
                }
            }
        }
    }

    private async void InstallSelectedApps_Click(object sender, RoutedEventArgs e)
    {
        var selected = _allApps.Where(a => a.IsSelected && (!a.IsInstalled || a.HasUpdate)).ToList();
        if (selected.Count == 0)
        {
            await ShowAlertAsync("Brak zaznaczenia", "Zaznacz programy do zainstalowania przy użyciu checkboxów.", "Rozumiem", "ℹ️");
            return;
        }

        string targetPath = _settingsService.Current.DefaultInstallFolder;
        bool confirm = await ShowConfirmAsync("Instalacja pakietu programów",
            $"Czy chcesz zainstalować {selected.Count} zaznaczonych programów?\nFolder docelowy: {targetPath}\n\nOperacja może potrwać kilka minut.", "Zainstaluj", "Anuluj");
        if (!confirm) return;

        AppendLog($"🚀 Rozpoczynam masową instalację {selected.Count} programów...");
        int success = 0;
        int failed = 0;

        foreach (var app in selected)
        {
            GlobalStatusText.Text = $"Instalowanie: {app.Name}...";
            bool ok = app.HasUpdate
                ? await _appInstaller.UpgradeAppAsync(app, AppendLog)
                : await _appInstaller.InstallAppAsync(app, targetPath, AppendLog);
            if (ok) success++; else failed++;
        }

        ApplyAppsFilter();
        GlobalStatusText.Text = $"Instalacja ukończona: {success} sukces, {failed} błędów.";
        await ShowAlertAsync("Wynik instalacji", $"Zakończono instalację pakietu programów!\n• Zainstalowano pomyślnie: {success}\n• Błędy: {failed}", "Zamknij", "ℹ️", isSuccess: failed == 0);
    }

    private async void UpgradeAllInstalledApps_Click(object sender, RoutedEventArgs e)
    {
        bool confirm = await ShowConfirmAsync("Automatyczna aktualizacja oprogramowania (Always Up-To-Date)",
            "Ta operacja sprawdzi i automatycznie zaktualizuje wszystkie zainstalowane programy na Twoim komputerze do najnowszych stabilnych wersji w tle (silent upgrade).\n\n" +
            "Czy chcesz rozpocząć pobieranie i instalację najnowszych wersji?", "⚡ Zaktualizuj wszystkie programy", "Anuluj");
        if (!confirm) return;

        LogDrawerBorder.Visibility = Visibility.Visible;
        ToggleLogButton.Content = "📋 Ukryj dziennik zdarzeń";
        AppendLog("===============================================================");
        AppendLog("🚀 ROZPOCZĘTO AUTOMATYCZNĄ AKTUALIZACJĘ WSZYSTKICH PROGRAMÓW 🚀");
        AppendLog("===============================================================");
        GlobalStatusText.Text = "Aktualizowanie programów do najnowszych wersji...";

        int count = 0;
        var toUpgrade = _allApps.Where(a => a.HasUpdate).ToList();
        foreach (var app in toUpgrade)
        {
            GlobalStatusText.Text = $"Aktualizowanie: {app.Name}...";
            bool ok = await _appInstaller.UpgradeAppAsync(app, AppendLog);
            if (ok) count++;
        }

        AppendLog("🔍 Sprawdzanie i automatyczna aktualizacja pozostałych pakietów systemowych...");
        var (upgradeOk, upgradeOutput) = await DiskHelper.RunProcessAsync("winget", "upgrade --all --silent --accept-package-agreements --accept-source-agreements", AppendLog);

        await _appInstaller.RefreshInstalledStatusesAsync(_allApps, _settingsService.Current.DefaultInstallFolder, AppendLog);
        ApplyAppsFilter();

        AppendLog("===============================================================");
        AppendLog("🎉 ZAKOŃCZONO PROCES AKTUALIZACJI OPROGRAMOWANIA!");
        AppendLog("===============================================================");
        GlobalStatusText.Text = upgradeOk ? "Zakończono polecenie aktualizacji. Sprawdź pozostałe aktualizacje." : "Aktualizacja zgłosiła błąd. Sprawdź dziennik.";

        await ShowAlertAsync("Wynik aktualizacji", $"Zaktualizowano {count} programów z katalogu. Pozostałe widoczne aktualizacje: {_allApps.Count(a => a.HasUpdate)}.\nWynik zbiorczego polecenia winget: {(upgradeOk ? "ukończono" : "błąd; sprawdź dziennik")}", "Zamknij", "ℹ️", isSuccess: upgradeOk);
    }

    // ==================== USTAWIENIA SYSTEMU & MODUŁY KLIENTSKIE ====================

    private void BrowseInstallFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Wybierz domyślny folder do instalacji programów (np. D:\\Programy)",
            InitialDirectory = Directory.Exists(_settingsService.Current.DefaultInstallFolder)
                ? _settingsService.Current.DefaultInstallFolder
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            SettingsInstallPathTextBox.Text = dialog.FolderName;
        }
    }

    private async void SaveInstallFolder_Click(object sender, RoutedEventArgs e)
    {
        string path = SettingsInstallPathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            await ShowAlertAsync("Błąd", "Ścieżka instalacji nie może być pusta.", "Popraw", "⚠️");
            return;
        }

        try
        {
            if (!Path.IsPathFullyQualified(path)) throw new IOException("Wybierz pełną ścieżkę folderu.");
            path = Path.GetFullPath(path);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            _settingsService.Current.DefaultInstallFolder = path;
            _settingsService.SaveSettings(_settingsService.Current);
            AppsTargetFolderText.Text = path;
            AppendLog($"💾 Zapisano domyślny katalog instalacji programów: {path}");
            await ShowAlertAsync("Ustawienie zapisane", $"Domyślny katalog instalacji został ustawiony na:\n{path}\n\nFolder jest gotowy do przyjmowania nowych programów.", "Gotowe", "✅", isSuccess: true);
        }
        catch (Exception ex)
        {
            await ShowAlertAsync("Błąd zapisu folderu", $"Nie można utworzyć lub zapisać folderu: {ex.Message}", "OK", "❌");
        }
    }

    private void SettingOption_Changed(object sender, RoutedEventArgs e)
    {
        if (!_settingsUiReady) return;
        _settingsService.Current.SilentInstall = OptSilentInstallCheck.IsChecked ?? true;
        _settingsService.Current.AutoCheckUpdates = OptAutoCheckUpdatesCheck.IsChecked ?? true;
        _settingsService.Current.RecycleBinDefault = OptRecycleBinCheck.IsChecked ?? true;
        RecycleBinCheckBox.IsChecked = _settingsService.Current.RecycleBinDefault;
        _settingsService.SaveSettings(_settingsService.Current);
    }

    private async void GenerateAuditReport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { FileName = $"Aetherial-raport-{DateTime.Now:yyyy-MM-dd}.html", Filter = "Raport HTML|*.html" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            await RefreshDiagnosticsAsync();
            var (total, available, load) = MemoryOptimizerService.GetMemoryMetrics();
            var drives = _scanner.GetDrives();
            static string Encode(string? text) => System.Net.WebUtility.HtmlEncode(text ?? "Brak danych");
            var rows = string.Join("", drives.Select(d => $"<tr><td>{Encode(d.DisplayName)}</td><td>{Encode(d.FormattedTotal)}</td><td>{Encode(d.FormattedFree)}</td><td>{d.UsedPercent}%</td></tr>"));
            var diagnostics = string.Join("", DiagnosticItems.Select(d => $"<li><strong>{Encode(d.Title)}</strong>: {Encode(d.BadgeText)}<p>{Encode(d.Description)}</p></li>"));
            string html = $$"""
                <!doctype html><html lang="pl"><meta charset="utf-8"><title>Raport Aetherial</title>
                <style>body{font:16px system-ui;max-width:1000px;margin:40px auto;padding:24px;color:#172033;background:#f4f6fa}table{border-collapse:collapse;width:100%}td,th{padding:12px;text-align:left;border-bottom:1px solid #ccd3df}p{line-height:1.6}</style>
                <h1>Raport odczytów systemowych</h1><p>{{DateTime.Now:yyyy-MM-dd HH:mm:ss}} • Aetherial {{typeof(App).Assembly.GetName().Version}}</p>
                <p>Administrator: {{(DiskHelper.IsAdministrator() ? "tak" : "nie")}}. RAM: {{DriveModel.FormatBytes((long)(total - available))}} / {{DriveModel.FormatBytes((long)total)}} ({{load}}%).</p>
                <h2>Pamięć masowa</h2><table><tr><th>Wolumin</th><th>Pojemność</th><th>Wolne</th><th>Wykorzystanie</th></tr>{{rows}}</table>
                <h2>Diagnostyka</h2><ul>{{diagnostics}}</ul>
                <p>Raport zawiera odczyty dostępne w chwili generowania. Nie stanowi certyfikatu sprawności ani pełnego audytu bezpieczeństwa.</p></html>
                """;
            await File.WriteAllTextAsync(dialog.FileName, html);
            GlobalStatusText.Text = "Zapisano raport odczytów systemowych.";
            AppendLog($"Zapisano raport: {dialog.FileName}");
            await ShowAlertAsync("Raport zapisany", dialog.FileName, "Zamknij", "📑", true);
        }
        catch (Exception ex) { await ShowAlertAsync("Nie zapisano raportu", ex.Message); }
    }

    private bool IsPrivacyShieldEnabled()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection");
            if (key != null)
            {
                var val = key.GetValue("AllowTelemetry");
                if (val is int i && i == 0) return true;
            }
        }
        catch { }
        return false;
    }

    private bool IsStorageSenseEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy");
            if (key != null)
            {
                var val = key.GetValue("01");
                if (val is int i && i == 1) return true;
            }
        }
        catch { }
        return false;
    }

    private void UpdateSystemToggleStates()
    {
        try
        {
            // 1. Tarcza Prywatności
            bool privacyOn = IsPrivacyShieldEnabled();
            if (PrivacyShieldStatusBadge != null && PrivacyShieldBadgeBorder != null && PrivacyShieldToggleButton != null)
            {
                if (privacyOn)
                {
                    PrivacyShieldStatusBadge.Text = "● WŁĄCZONA (ON)";
                    PrivacyShieldStatusBadge.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                    PrivacyShieldBadgeBorder.SetResourceReference(Border.BackgroundProperty, "AccentSoftBrush");
                    PrivacyShieldBadgeBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#166534"));
                    PrivacyShieldToggleButton.Content = "🛡️ Wyłącz Tarczę (Włącz diagnostykę)";
                    PrivacyShieldToggleButton.Style = (Style)FindResource("SecondaryButtonStyle");
                }
                else
                {
                    PrivacyShieldStatusBadge.Text = "○ WYŁĄCZONA (OFF)";
                    PrivacyShieldStatusBadge.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                    PrivacyShieldBadgeBorder.SetResourceReference(Border.BackgroundProperty, "AccentSoftBrush");
                    PrivacyShieldBadgeBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#78350F"));
                    PrivacyShieldToggleButton.Content = "🛡️ Włącz Tarczę Prywatności";
                    PrivacyShieldToggleButton.Style = (Style)FindResource("PrimaryButtonStyle");
                }
            }

            // 2. Hibernacja
            string hiberPath = Path.Combine(SystemDrive, "hiberfil.sys");
            bool hiberOn = File.Exists(hiberPath);
            if (HibernationStatusBadge != null && HibernationBadgeBorder != null)
            {
                if (hiberOn)
                {
                    HibernationStatusBadge.Text = $"● WŁĄCZONA ({DriveModel.FormatBytes(DiskHelper.GetFileSize(hiberPath))})";
                    HibernationStatusBadge.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                    HibernationBadgeBorder.SetResourceReference(Border.BackgroundProperty, "AccentSoftBrush");
                    HibernationBadgeBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#78350F"));
                }
                else
                {
                    HibernationStatusBadge.Text = "Nie wykryto pliku hibernacji";
                    HibernationStatusBadge.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                    HibernationBadgeBorder.SetResourceReference(Border.BackgroundProperty, "AccentSoftBrush");
                    HibernationBadgeBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#166534"));
                }
            }

            // 3. Czujnik Pamięci (Storage Sense)
            bool storageSenseOn = IsStorageSenseEnabled();
            if (StorageSenseStatusBadge != null && StorageSenseBadgeBorder != null && StorageSenseToggleButton != null)
            {
                if (storageSenseOn)
                {
                    StorageSenseStatusBadge.Text = "● WŁĄCZONY (ON)";
                    StorageSenseStatusBadge.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                    StorageSenseBadgeBorder.SetResourceReference(Border.BackgroundProperty, "AccentSoftBrush");
                    StorageSenseBadgeBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#166534"));
                    StorageSenseToggleButton.Content = "🧹 Wyłącz Czujnik Pamięci";
                }
                else
                {
                    StorageSenseStatusBadge.Text = "○ WYŁĄCZONY (OFF)";
                    StorageSenseStatusBadge.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                    StorageSenseBadgeBorder.SetResourceReference(Border.BackgroundProperty, "AccentSoftBrush");
                    StorageSenseBadgeBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E3146"));
                    StorageSenseToggleButton.Content = "🧹 Włącz Czujnik Pamięci";
                }
            }
        }
        catch { }
    }

    private async void TogglePrivacyShield_Click(object sender, RoutedEventArgs e)
    {
        bool isCurrentlyOn = IsPrivacyShieldEnabled();
        string actionText = isCurrentlyOn
            ? "wyłączyć Tarczę Prywatności i włączyć usługi diagnostyczne Windows"
            : "włączyć Tarczę Prywatności i zablokować telemetrię DiagTrack, śledzenie reklamowe oraz zbędne usługi diagnostyczne";

        bool confirm = await ShowConfirmAsync("Tarcza Prywatności Windows 11",
            $"Czy na pewno chcesz {actionText}?", isCurrentlyOn ? "Wyłącz Tarczę" : "Włącz Tarczę", "Anuluj");
        if (!confirm) return;

        LogDrawerBorder.Visibility = Visibility.Visible;
        ToggleLogButton.Content = "📋 Ukryj dziennik zdarzeń";

        try
        {
            if (isCurrentlyOn)
            {
                AppendLog("🛡️ Przywracanie domyślnych usług Windows...");
                GlobalStatusText.Text = "Włączanie usług diagnostycznych Windows...";
                var (configured, _) = await DiskHelper.RunProcessAsync("sc", "config DiagTrack start= auto", AppendLog);
                if (!configured) throw new IOException("Nie zmieniono konfiguracji DiagTrack. Sprawdź dziennik.");
                await DiskHelper.RunProcessAsync("sc", "start DiagTrack", AppendLog);
                await DiskHelper.RunProcessAsync("sc", "config dmwappushservice start= demand", AppendLog);
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 1, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 1, RegistryValueKind.DWord);
                AppendLog("ℹ️ Przywrócono domyślne ustawienia usług diagnostycznych.");
                GlobalStatusText.Text = "Tarcza prywatności wyłączona.";
                await ShowAlertAsync("Tarcza Wyłączona", "Zapisano ustawienia włączające diagnostykę i identyfikator reklamowy. Wyniki uruchomienia usług są w dzienniku.", "OK", "ℹ️");
            }
            else
            {
                AppendLog("🛡️ Aktywacja Tarczy Prywatności Windows 11...");
                GlobalStatusText.Text = "Wyłączanie telemetrii Windows 11...";
                await DiskHelper.RunProcessAsync("sc", "stop DiagTrack", AppendLog);
                var (configured, _) = await DiskHelper.RunProcessAsync("sc", "config DiagTrack start= disabled", AppendLog);
                if (!configured) throw new IOException("Nie zmieniono konfiguracji DiagTrack. Sprawdź dziennik.");
                await DiskHelper.RunProcessAsync("sc", "stop dmwappushservice", AppendLog);
                await DiskHelper.RunProcessAsync("sc", "config dmwappushservice start= disabled", AppendLog);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0, RegistryValueKind.DWord);
                AppendLog("✅ Tarcza Prywatności Windows 11 została pomyślnie zastosowana!");
                GlobalStatusText.Text = "Tarcza prywatności aktywna. Telemetria wyłączona.";
                await ShowAlertAsync("Tarcza Prywatności Aktywna", "Zapisano politykę AllowTelemetry=0 i wyłączono identyfikator reklamowy. Wyniki poleceń usług są w dzienniku. Zakres ograniczenia diagnostyki zależy od wersji Windows.", "Super", "🛡️", isSuccess: true);
            }
        }
        catch (Exception ex)
        {
            AppendLog($"⚠️ Błąd przełączania tarczy: {ex.Message}");
            await ShowAlertAsync("Nie ukończono zmiany", ex.Message, "OK", "⚠️");
        }
        finally
        {
            UpdateSystemToggleStates();
        }
    }

    private async void ToggleStorageSense_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            bool current = IsStorageSenseEnabled();
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy");
            key.SetValue("01", current ? 0 : 1, RegistryValueKind.DWord);
            AppendLog($"🧹 Zmieniono stan Czujnika Pamięci (Storage Sense) na: {(!current ? "WŁĄCZONY" : "WYŁĄCZONY")}.");
            UpdateSystemToggleStates();
            await ShowAlertAsync("Czujnik Pamięci", $"Pomyślnie {(!current ? "włączono" : "wyłączono")} automatyczny Czujnik Pamięci Windows 11!", "OK", "🧹", isSuccess: true);
        }
        catch (Exception ex)
        {
            await ShowAlertAsync("Błąd", ex.Message, "OK", "⚠️");
        }
    }

    private async void ManageStartup_Click(object sender, RoutedEventArgs e)
    {
        AppendLog("⚡ Skanowanie wpisów autostartu systemu...");
        GlobalStatusText.Text = "Skanowanie programów uruchamianych przy starcie...";

        var startupList = new List<string>();

        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
            {
                if (key != null)
                {
                    foreach (var name in key.GetValueNames())
                    {
                        startupList.Add($"[Użytkownik] {name}: {key.GetValue(name)}");
                    }
                }
            }

            using (var key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
            {
                if (key != null)
                {
                    foreach (var name in key.GetValueNames())
                    {
                        startupList.Add($"[System] {name}: {key.GetValue(name)}");
                    }
                }
            }
        }
        catch { }

        AppendLog($"⚡ Wykryto {startupList.Count} programów w autostarcie.");
        GlobalStatusText.Text = $"Wykryto {startupList.Count} programów w autostarcie.";

        string msg = $"Wykryto {startupList.Count} programów uruchamiających się ze startem Windows:\n\n" +
                     string.Join("\n", startupList.Take(6)) +
                     (startupList.Count > 6 ? $"\n...oraz {startupList.Count - 6} więcej." : "") +
                     "\n\nCzy chcesz otworzyć menedżer Autostartu Windows, aby wyłączyć zbędne wpisy?";

        bool open = await ShowConfirmAsync("Analizator Autostartu", msg, "Otwórz Menedżer Startu", "Zamknij");
        if (open)
        {
            try
            {
                Process.Start("taskmgr.exe");
            }
            catch { }
        }
    }

    private async void CheckNvmeHealth_Click(object sender, RoutedEventArgs e)
    {
        AppendLog("🩺 Sprawdzanie kondycji i parametrów S.M.A.R.T. dysków SSD/NVMe...");
        GlobalStatusText.Text = "Odczyt S.M.A.R.T. dysków NVMe...";

        var (ok, output, _) = await DiskHelper.RunPowerShellScriptAsync(
            "Get-PhysicalDisk | Select-Object DeviceId, FriendlyName, MediaType, OperationalStatus, HealthStatus, Size | Format-Table -AutoSize | Out-String",
            AppendLog);

        GlobalStatusText.Text = ok ? "Odczytano stan dysków." : "Odczyt stanu dysków nie powiódł się.";
        string details = string.IsNullOrWhiteSpace(output) ? "Brak danych o kondycji dysków." : output.Trim();

        await ShowAlertAsync("Kondycja Dysków (S.M.A.R.T.)",
            $"Parametry raportowane przez Windows:\n\n{details}\n\nTo podsumowanie Get-PhysicalDisk, a nie pełny test S.M.A.R.T.", "Zamknij", "🩺", isSuccess: ok);
    }

    private void CloseOptimizedOverlay_Click(object sender, RoutedEventArgs e)
    {
        _optimizedTimer?.Stop();
        if (FullOptimizedOverlay != null)
            FullOptimizedOverlay.Visibility = Visibility.Collapsed;
    }

    private void OverlayNavToDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        _optimizedTimer?.Stop();
        if (FullOptimizedOverlay != null)
            FullOptimizedOverlay.Visibility = Visibility.Collapsed;
        NavDriversRadio.IsChecked = true;
        SwitchView(3);
    }

    private async void RestartBluetoothService_Click(object sender, RoutedEventArgs e)
    {
        AppendLog("▶ Restartowanie usługi Bluetooth (bthserv)...");
        var (ok, _, _) = await DiskHelper.RunPowerShellScriptAsync("Restart-Service bthserv -Force -ErrorAction Stop", AppendLog);
        await ShowAlertAsync("Bluetooth", "Wysłano polecenie restartu usługi Bluetooth. Sprawdź działanie urządzeń bezprzewodowych.", "OK", "🔄", isSuccess: ok);
    }

    private void OpenAsrockBios_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("msinfo32.exe") { UseShellExecute = true });
        }
        catch { }
    }

    private async void RunTrimNow_Click(object sender, RoutedEventArgs e)
    {
        AppendLog("▶ Uruchamianie procedury ReTrim dla partycji SSD...");
        var (ok, _, _) = await DiskHelper.RunPowerShellScriptAsync("$ErrorActionPreference='Stop'; Get-Volume | Where-Object { $_.DriveType -eq 'Fixed' -and $_.DriveLetter } | ForEach-Object { Optimize-Volume -DriveLetter $_.DriveLetter -ReTrim -Verbose }", AppendLog);
        await ShowAlertAsync("SSD TRIM", ok ? "Windows zakończył polecenie ReTrim. Szczegóły znajdują się w dzienniku." : "Nie ukończono ReTrim. Sprawdź błędy w dzienniku.", "Zamknij", "⚡", isSuccess: ok);
    }

    private async void InstallMediaTekDrivers_Click(object sender, RoutedEventArgs e)
    {
        AppendLog("▶ Uruchamianie instalatora sterowników MediaTek Wi-Fi 6E i Bluetooth...");
        bool launched = DriverUpdaterService.InstallMediaTekDrivers(AppendLog);
        if (launched)
        {
            await ShowAlertAsync(
                "Instalator Sterowników MediaTek",
                "Uruchomiono instalację sterowników MediaTek Wi-Fi 6E (mtkwl6ex.inf) oraz Bluetooth (mtkbtfilter.inf) z uprawnieniami administratora.\n\nPo kliknięciu 'Tak' w oknie UAC sterowniki zostaną zainstalowane w systemie.",
                "Zrozumiałem",
                "📶",
                isSuccess: true);
        }
        else
        {
            await ShowAlertAsync(
                "Instalator Sterowników",
                "Nie udało się automatycznie uruchomić instalatora. Skrypt 'Zainstaluj_Sterowniki_MediaTek.bat' znajduje się w folderze Pobrane.",
                "OK",
                "⚠️",
                isSuccess: false);
        }
    }
}
