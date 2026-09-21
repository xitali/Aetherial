using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DiskOptimizer.ViewModels;
using DiskOptimizer;
using DiskOptimizer.Models;
using DiskOptimizer.Services;
using DiskOptimizer.Views;
using System.Collections.ObjectModel;

internal static class Program
{
    private static readonly (string View, string Navigation, int Index)[] Routes =
    {
        ("ViewCleaner", "NavCleanerRadio", 1), ("ViewExplorer", "NavExplorerRadio", 2),
        ("ViewDrivers", "NavDriversRadio", 3), ("ViewApps", "NavAppsRadio", 5),
        ("ViewTools", "NavToolsRadio", 4), ("ViewHistory", "NavHistoryRadio", 8),
        ("ViewSettings", "NavSettingsRadio", 6)
    };
    private static readonly (string Tile, string Section)[] ToolRoutes =
    {
        ("ToolMigrationTile", "ToolMigrationSection"), ("ToolDevTile", "ToolDevSection"),
        ("ToolSystemTile", "ToolSystemSection"), ("ToolMemoryTile", "ToolMemorySection")
    };

    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            // Instantiate the actual WPF view without Show/Loaded or startup operations.
            var app = new Application();
            // Load the real theme without the production app's queued startup.
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Aetherial;component/Themes/Controls.xaml", UriKind.Relative) });
            var bindingErrors = new StringWriter();
            PresentationTraceSources.DataBindingSource.Listeners.Add(new TextWriterTraceListener(bindingErrors));
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
            var window = new MainWindow();
            var root = (FrameworkElement)window.Content;
            foreach (var item in new DiskScannerService().GetDefaultItems()) { item.IsSelected = false; window.CleanItems.Add(item); }
            typeof(MainWindow).GetMethod("InitializeAppsCatalog", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
            foreach (var item in new SymlinkService().GetDefaultPresets()) window.SymlinkPresets.Add(item);
            var output = Path.GetFullPath(args.FirstOrDefault(a => !a.StartsWith("--")) ?? ".artifacts/visual");
            Directory.CreateDirectory(output);
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
                window.Drives.Add(new DriveModel { DriveLetter = drive.Name, VolumeLabel = drive.VolumeLabel, TotalBytes = drive.TotalSize, FreeBytes = drive.AvailableFreeSpace });

            VerifyNavigation(window, root);
            VerifyExplorer(window, root);
            VerifyCleaner();
            VerifyPrograms(window, root);
            VerifyToolsAndPreferences(window, root);
            if (args.Contains("--interactions-only"))
            {
                PresentationTraceSources.DataBindingSource.Flush();
                if (bindingErrors.ToString().Contains("Error:")) throw new InvalidOperationException(bindingErrors.ToString());
                Console.WriteLine("PASS WPF binding error trace.");
                return 0;
            }
            SynchronizationContext.SetSynchronizationContext(null);
            var inventory = new SoftwareInstallerService().GetInstalledAppsAsync().GetAwaiter().GetResult();
            typeof(MainWindow).GetField("_installedApps", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(window, inventory.Apps.ToList());
            typeof(MainWindow).GetField("_appsReadCompleted", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(window, true);
            typeof(MainWindow).GetField("_appsUpdatesChecked", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(window, inventory.UpdatesChecked);
            ((TextBlock)window.FindName("AppsStatusText")).Text = inventory.Status;
            typeof(MainWindow).GetMethod("ApplyAppsFilter", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
            Console.WriteLine($"LIVE software inventory: {inventory.Apps.Count} records, complete={inventory.IsComplete}, updatesChecked={inventory.UpdatesChecked}; updates={inventory.Apps.Count(a => a.HasUpdate)}.");
            var mem = MemoryOptimizerService.GetMemoryMetrics();
            ((TextBlock)window.FindName("RamStatusText")).Text = $"{mem.loadPercent}% • {DriveModel.FormatBytes((long)mem.availBytes)} dostępne";
            ((TextBlock)window.FindName("TopUptimeText")).Text = TimeSpan.FromMilliseconds(Environment.TickCount64).ToString(@"d\d\ hh\h\ mm\m");
            var hardware = new DriverUpdaterService().GetHardwareSpecsAsync().GetAwaiter().GetResult();
            ((TextBlock)window.FindName("DashboardCpuText")).Text = hardware.CpuName;
            ((TextBlock)window.FindName("DashboardGpuText")).Text = hardware.GpuPrimary;
            ((TextBlock)window.FindName("DashboardNetworkText")).Text = hardware.Network;
            ((TextBlock)window.FindName("HardwareSummaryText")).Text = hardware.Motherboard + " • " + hardware.Ram;
            foreach (var device in new DriverUpdaterService().GetConnectedPnpDevicesAsync().GetAwaiter().GetResult()) window.PnpDevices.Add(device);
            var assessments = new DriverCatalogService().AssessAsync(window.PnpDevices).GetAwaiter().GetResult();
            ((HardwareViewModel)((FrameworkElement)window.FindName("HardwarePanel")).DataContext).LoadResults(assessments);
            Console.WriteLine($"LIVE driver catalogue: {assessments.Count} devices, {assessments.Count(a => a.HasUpdate)} updates, {assessments.Count(a => a.Status == DriverAssessmentStatus.Error)} source errors.");
            ((TextBlock)window.FindName("HardwareInventorySummaryText")).Text = $"{window.PnpDevices.Count} urządzeń z Windows CIM";
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            window.ScanResultItems.Add(new ScanResultItem { Name = "Tymczasowe pliki systemowe", Description = "Kosz i cache Windows", SizeBytes = 1024 * 1024 * 350, Severity = Severity.Warning });
            window.HistoryItems.Add(new HistoryEntry { OperationType = "Czyszczenie dysku", BytesSaved = 1024 * 1024 * 500, ItemsFixed = 3, Success = true, Summary = "Usunięto zbędne pliki cache" });
            var switchView = typeof(MainWindow).GetMethod("SwitchView", BindingFlags.NonPublic | BindingFlags.Instance)!;
            int count = 0;
            foreach (bool light in new[] { false, true })
            {
                App.ApplyTheme(light);
                foreach (var route in Routes)
                {
                    switchView.Invoke(window, new object[] { route.Index });
                    Render(root, output, $"{(light ? "light" : "dark")}-{route.View}-1366", 1366, 900, 1);
                    count++;
                    if (route.View == "ViewSettings")
                    {
                        foreach (var topic in new[] { "SettingsAppsTab", "SettingsWindowsTab", "SettingsAppearanceTab" })
                        {
                            ((Button)window.FindName(topic)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            Render(root, output, $"{(light ? "light" : "dark")}-{topic}-1180", 1180, 820, 1);
                            count++;
                        }
                    }
                    if (route.View is "ViewCleaner" or "ViewExplorer" or "ViewDrivers" or "ViewApps" or "ViewHistory")
                    {
                        Render(root, output, $"{(light ? "light" : "dark")}-{route.View}-1180", 1180, 820, 1);
                        count++;
                    }
                    if (route.View == "ViewTools")
                    {
                        Render(root, output, $"{(light ? "light" : "dark")}-tools-home-1180", 1180, 820, 1);
                        count++;
                        foreach (var tool in ToolRoutes)
                        {
                            ((Button)window.FindName(tool.Tile)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            foreach (int width in new[] { 1180, 1366 })
                            {
                                Render(root, output, $"{(light ? "light" : "dark")}-{tool.Section}-{width}", width, 820, 1);
                                count++;
                            }
                            BackToTools(window);
                        }
                    }
                }
                switchView.Invoke(window, new object[] { 1 });
                count += RenderCleanerStates(window, root, output, light);
                var cleaner = (CleanerView)window.FindName("CleanerPanel");
                cleaner.SetScanState(true); cleaner.SetScanState(false);
                Render(root, output, $"{(light ? "light" : "dark")}-cleaner-start-1440p", 2560, 1440, 1);
                Render(root, output, $"{(light ? "light" : "dark")}-cleaner-start-4k-200dpi", 1920, 1080, 2);
                count += 2;
            }
            Console.WriteLine($"PASS {count} WPF renders. Real local volume/hardware reads; cleaner result/history fixtures exist only in this harness. No destructive handlers invoked.");
            VerifyNavigation(window, root, verifyInitial: false);
            Console.WriteLine($"PASS all {Routes.Length} navigation routes; Light/Dark resources load.");
            PresentationTraceSources.DataBindingSource.Flush();
            if (bindingErrors.ToString().Contains("Error:")) throw new InvalidOperationException(bindingErrors.ToString());
            Console.WriteLine("PASS WPF binding error trace.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void VerifyNavigation(MainWindow window, FrameworkElement root, bool verifyInitial = true)
    {
        if (verifyInitial)
        {
            Check(((FrameworkElement)window.FindName("ViewCleaner")).Visibility == Visibility.Visible, "Cleaner is the startup screen");
            Check(((RadioButton)window.FindName("NavCleanerRadio")).IsChecked == true, "Startup navigation selects Cleaner");
        }
        foreach (int width in new[] { 1180, 1366 })
        foreach (var route in Routes.Concat(Routes.Reverse()))
        {
            ((RadioButton)window.FindName(route.Navigation)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Layout(root, width, 820);
            Check(Routes.Count(r => ((FrameworkElement)window.FindName(r.View)).Visibility == Visibility.Visible) == 1, "Exactly one section visible after " + route.Navigation);
            Check(((FrameworkElement)window.FindName(route.View)).Visibility == Visibility.Visible, "Navigation opens " + route.View);
            Check(Routes.Count(r => ((RadioButton)window.FindName(r.Navigation)).IsChecked == true) == 1, "Exactly one selected navigation item after " + route.Navigation + ": " + string.Join(", ", Routes.Select(r => r.Navigation + "=" + ((RadioButton)window.FindName(r.Navigation)).IsChecked)));
            foreach (var other in Routes)
            {
                var nav = (RadioButton)window.FindName(other.Navigation);
                Check(VisibleWithin(nav, root) && nav.ActualWidth > 0 && nav.ActualHeight > 0, "Every section remains reachable: " + other.Navigation);
                var bounds = nav.TransformToAncestor(root).TransformBounds(new Rect(0, 0, nav.ActualWidth, nav.ActualHeight));
                Check(bounds.Left >= 0 && bounds.Right <= width && bounds.Top >= 0 && bounds.Bottom <= 820, "Navigation remains inside window: " + other.Navigation);
            }
            foreach (var legacy in new[] { "LegacyStatusHeader", "LegacyFooter", "ViewDashboard", "ViewScanner" })
                Check(((FrameworkElement)window.FindName(legacy)).Visibility == Visibility.Collapsed, "Legacy duplicated surface remains hidden: " + legacy);
        }
        var switchView = typeof(MainWindow).GetMethod("SwitchView", BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (int legacy in new[] { 0, 7 })
        {
            switchView.Invoke(window, new object[] { legacy });
            Check(((FrameworkElement)window.FindName("ViewCleaner")).Visibility == Visibility.Visible && ((RadioButton)window.FindName("NavCleanerRadio")).IsChecked == true, "Legacy route redirects to Cleaner: " + legacy);
        }
        Console.WriteLine("PASS 7 persistent sidebar destinations, legacy route aliases and hidden duplicate bars at 1180/1366.");
    }

    private static void VerifyToolsAndPreferences(MainWindow window, FrameworkElement root)
    {
        ((RadioButton)window.FindName("NavToolsRadio")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        foreach (var tool in ToolRoutes)
        {
            Layout(root, 1180, 820);
            Check(((FrameworkElement)window.FindName("ToolHomePanel")).Visibility == Visibility.Visible, "Tools start with categories");
            ((Button)window.FindName(tool.Tile)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Layout(root, 1180, 820);
            Check(((FrameworkElement)window.FindName("ToolHomePanel")).Visibility == Visibility.Collapsed && ((FrameworkElement)window.FindName("ToolDetailPanel")).Visibility == Visibility.Visible, "Tool selection opens its detail");
            Check(ToolRoutes.Count(t => ((FrameworkElement)window.FindName(t.Section)).Visibility == Visibility.Visible) == 1 && ((FrameworkElement)window.FindName(tool.Section)).Visibility == Visibility.Visible, "Only selected tool is visible: " + tool.Section);
            BackToTools(window);
            Layout(root, 1180, 820);
            foreach (var next in ToolRoutes)
                Check(VisibleWithin((Button)window.FindName(next.Tile), root), "Back restores access to every tool: " + next.Tile);
        }
        ((RadioButton)window.FindName("NavSettingsRadio")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var settings = App.Services.GetService(typeof(SettingsService)) as SettingsService ?? throw new Exception("Missing settings");
        foreach (var name in new[] { "SettingsAppsTab", "SettingsWindowsTab", "SettingsAppearanceTab" })
        {
            ((Button)window.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(new[] { "SettingsAppearancePage", "SettingsAppsPage", "SettingsWindowsPage" }.Count(p => ((FrameworkElement)window.FindName(p)).Visibility == Visibility.Visible) == 1, "Exactly one settings topic visible");
        }
        var original = settings.Current.MetricsRefreshSeconds;
        var oldThreshold = settings.Current.LargeFileThresholdMb;
        try
        {
            settings.Current.MetricsRefreshSeconds = 9;
            settings.Current.LargeFileThresholdMb = 1234;
            typeof(MainWindow).GetMethod("ApplyLivePreferences", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
            var timer = (DispatcherTimer)typeof(MainWindow).GetField("_metricsTimer", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
            var explorer = (ExplorerViewModel)typeof(MainWindow).GetField("_explorerViewModel", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
            Check(timer.Interval == TimeSpan.FromSeconds(9), "Preference updates actual metrics timer");
            Check(explorer.LargeFileThresholdMb == 1234, "Preference updates actual file search threshold");
        }
        finally { settings.Current.MetricsRefreshSeconds = original; settings.Current.LargeFileThresholdMb = oldThreshold; typeof(MainWindow).GetMethod("ApplyLivePreferences", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null); }
        foreach (var orientation in new[] { Orientation.Vertical, Orientation.Horizontal })
        {
            var bar = new System.Windows.Controls.Primitives.ScrollBar { Style = (Style)App.Current.FindResource(typeof(System.Windows.Controls.Primitives.ScrollBar)), Orientation = orientation, Minimum = 0, Maximum = 100, ViewportSize = 20, Value = 40 };
            bar.Measure(new Size(300, 300)); bar.Arrange(new Rect(0, 0, 300, 300)); bar.ApplyTemplate();
            var track = (System.Windows.Controls.Primitives.Track)bar.Template.FindName("PART_Track", bar);
            Check(track != null && track.Thumb != null, "Scrollbar exposes functional track and thumb");
            bar.Value = 75;
            Check(Math.Abs(track!.Value - 75) < 0.01, "Scrollbar tracks value in " + orientation);
            var delta = new System.Windows.Controls.Primitives.DragDeltaEventArgs(20, 20) { RoutedEvent = System.Windows.Controls.Primitives.Thumb.DragDeltaEvent };
            track!.Thumb!.RaiseEvent(delta);
            Check(bar.Value > 75, "Dragging scrollbar thumb changes value in " + orientation);
        }
        Console.WriteLine("PASS 4 tool detail/back routes, applied settings, vertical/horizontal scrollbar templates.");
    }

    private static void VerifyCleaner()
    {
        var view = new CleanerView();
        using var vm = view.ViewModel;
        var item = new CleanItem { Id = "recycle_bin", Title = "Kosz — test interfejsu", Description = "Opis widoczny dopiero po otwarciu szczegółów.", Category = "System", CanClean = true, IsSelected = false, SizeBytes = 4096, Path = "test-only/no-files-are-deleted" };
        var empty = new CleanItem { Id = "empty-fixture", Title = "Pusta kategoria testowa", Category = "System", CanClean = true, IsSelected = false };
        view.SetItems(new ObservableCollection<CleanItem> { item, empty });
        Layout(view, 900, 700);
        VerifyCleanerPanel(view, "IdlePanel");
        Check(VisibleButtons(view).Length == 1 && VisibleButtons(view)[0].Name == "StartScanButton", "Idle presents only the scan action");
        int scanEvents = 0, cancelEvents = 0, cleanEvents = 0;
        view.ScanRequested += (_, _) => scanEvents++;
        view.CancelRequested += (_, _) => cancelEvents++;
        view.CleanRequested += (_, _) => cleanEvents++;
        ((Button)view.FindName("StartScanButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(scanEvents == 1, "Scan CTA emits the scan request");
        Check(!vm.CanClean, "Cleaner cannot run before a completed scan");
        vm.SetScanState(true);
        Layout(view, 900, 700);
        VerifyCleanerPanel(view, "BusyPanel");
        Check(!vm.CanScan && !vm.CanClean, "Scanning blocks duplicate scans and cleaning");
        VisibleButtons(view).Single(b => Equals(b.Content, "Anuluj")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(cancelEvents == 1, "Scanning cancellation is connected");
        item.IsScanned = true;
        Layout(view, 900, 700);
        var progress = Descendants((FrameworkElement)view.FindName("BusyPanel")).OfType<ProgressBar>().Single();
        Check(Math.Abs(progress.Value - 50) < 0.01 && !progress.IsIndeterminate, "Scan progress reflects completed categories");
        empty.IsScanned = true;
        vm.SetScanCompleted();
        Layout(view, 900, 700);
        VerifyCleanerPanel(view, "ResultsPanel");
        Check(!vm.CanClean && !((Button)view.FindName("ReviewCleanButton")).IsEnabled, "Review remains disabled until data is selected");
        Check(vm.ItemsView.Cast<CleanItem>().SequenceEqual(new[] { item }), "Empty categories are hidden by default");
        vm.ShowAllCategories = true;
        Check(vm.ItemsView.Cast<CleanItem>().Count() == 2, "Optional category disclosure includes empty results");
        vm.ShowAllCategories = false;
        Layout(view, 900, 700);
        var details = Descendants(view).OfType<ToggleButton>().Single(t => t.Name == "DetailsToggle" && ReferenceEquals(t.DataContext, item));
        var description = Descendants(view).OfType<TextBlock>().Single(t => t.Text == item.Description);
        var warningText = (string)new CleanItemWarningConverter().Convert(item, typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture);
        var warning = Descendants(view).OfType<TextBlock>().Single(t => t.Text == warningText);
        Check(details.IsChecked != true && !VisibleWithin(description, view), "Technical details are collapsed by default");
        Check(!VisibleWithin(warning, view), "Unselected category does not show its contextual warning");
        details.IsChecked = true;
        Layout(view, 900, 700);
        Check(VisibleWithin(description, view), "Details disclosure reveals the category description");
        details.IsChecked = false;
        item.IsSelected = true;
        Layout(view, 900, 700);
        warning = Descendants(view).OfType<TextBlock>().Single(t => t.Text == warningText);
        Check(VisibleWithin(warning, view), "Selecting Recycle Bin shows irreversible-deletion warning");
        Check(vm.CanClean && vm.Progress == 100, "Completed selected scan enables review");
        ((Button)view.FindName("ReviewCleanButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(cleanEvents == 1, "Review emits a request without deleting data in the harness");
        vm.SetCleaningState(true);
        Layout(view, 900, 700);
        VerifyCleanerPanel(view, "BusyPanel");
        Check(progress.IsIndeterminate && !vm.CanClean && !vm.CanSelect, "Cleaning has its own progress state and blocks repeat execution");
        VisibleButtons(view).Single(b => Equals(b.Content, "Anuluj")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(cancelEvents == 2, "Cleaning cancellation is connected");
        vm.SetResult(4096, 2, 0);
        Layout(view, 900, 700);
        VerifyCleanerPanel(view, "OutcomePanel");
        Check(vm.State == CleanerState.Done && vm.OutcomeSize == DriveModel.FormatBytes(4096) && VisibleButtons(view).Length == 1, "Successful outcome shows measured result and one next action");
        vm.SetResult(2048, 1, 1);
        Layout(view, 900, 700);
        VerifyCleanerPanel(view, "OutcomePanel");
        Check(vm.State == CleanerState.Error && vm.ResultText.Contains("1") && vm.ResultText.Contains("Błędy"), "Partial outcome retains its error count");
        vm.SetScanState(true);
        Check(!item.IsScanned && !vm.CanClean && vm.Progress == 0, "Rescan clears old completion and disables cleaning");
        vm.SetScanState(false);
        Check(!vm.CanClean && !item.IsScanned && vm.State == CleanerState.Idle, "Canceled rescan cannot reuse stale selection");
        vm.SetScanState(true);
        item.IsScanned = true;
        vm.SetScanState(false);
        Check(!vm.CanClean, "Partially completed canceled scan cannot authorize cleaning");
        Console.WriteLine("PASS cleaner idle/scanning/results/cleaning/outcome views, contextual disclosure, event wiring and canceled/stale-scan guards.");
    }

    private static void VerifyCleanerPanel(CleanerView view, string expected)
    {
        var panels = new[] { "IdlePanel", "BusyPanel", "ResultsPanel", "OutcomePanel" };
        Check(panels.Count(p => ((FrameworkElement)view.FindName(p)).Visibility == Visibility.Visible) == 1
            && ((FrameworkElement)view.FindName(expected)).Visibility == Visibility.Visible, "Only the current cleaner step is visible: " + expected);
    }

    private static void VerifyPrograms(MainWindow window, FrameworkElement root)
    {
        var inventory = typeof(MainWindow).GetField("_installedApps", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var original = inventory.GetValue(window);
        var update = new AppPackageItem { Id = "Fixture.Tool", Name = "Aktualizacja testowa", Source = "winget", InventoryOnly = true, IsPackageIdVerified = true, IsInstalled = true, HasUpdate = true, InstalledVersion = "1.0", AvailableVersion = "2.0" };
        var unknown = new AppPackageItem { Id = "Registry:fixture", Name = "Niezweryfikowany program", InventoryOnly = true, IsInstalled = true };
        try
        {
            inventory.SetValue(window, new List<AppPackageItem> { update, unknown });
            ((RadioButton)window.FindName("NavAppsRadio")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ((Button)window.FindName("AppsInstalledTab")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Layout(root, 1180, 820);
            Check(window.AppsList.Count == 2, "Installed apps view includes records beyond the catalogue");
            Check(!unknown.ActionButtonVisible && !unknown.CanAction, "Unverified installed record cannot become an install target");
            ((Button)window.FindName("AppsUpdatesTab")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(window.AppsList.Count == 1 && ReferenceEquals(window.AppsList[0], update), "Updates view contains only available updates");
            var search = (TextBox)window.FindName("AppsSearchTextBox");
            search.Text = "nieistniejący-wpis";
            Check(window.AppsList.Count == 0 && ((FrameworkElement)window.FindName("AppsEmptyText")).Visibility == Visibility.Visible, "Program search has an explicit empty state");
            search.Clear();
            ((Button)window.FindName("AppsCatalogTab")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(window.AppsList.Count > 2 && window.AppsList.All(a => !a.InventoryOnly), "Optional catalogue is separate from installed inventory");
        }
        finally
        {
            inventory.SetValue(window, original);
            ((Button)window.FindName("AppsInstalledTab")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
        Console.WriteLine("PASS programs inventory/update/catalogue navigation, search and unverified action guards.");
    }

    private static int RenderCleanerStates(MainWindow window, FrameworkElement root, string output, bool light)
    {
        // These values are deliberately isolated UI fixtures; no scanner or deletion operation consumes them.
        var view = (CleanerView)window.FindName("CleanerPanel");
        var fixture = new ObservableCollection<CleanItem>
        {
            new() { Id = "user_temp", Title = "Tymczasowe pliki użytkownika", Category = "System", Description = "Pliki tymczasowe utworzone przez aplikacje. Zamknij działające instalatory.", Path = "Przykładowa ścieżka — wyłącznie test renderowania", SizeBytes = 460 * 1024 * 1024, CanClean = true, IsSelected = true },
            new() { Id = "recycle_bin", Title = "Kosz", Category = "System", Description = "Usunięte wcześniej pliki użytkownika.", SizeBytes = 86 * 1024 * 1024, CanClean = true, IsSelected = false },
            new() { Id = "chrome_cache", Title = "Pamięć podręczna Chrome", Category = "Przeglądarki", Description = "Pliki stron internetowych przechowywane na dysku.", SizeBytes = 220 * 1024 * 1024, CanClean = true, IsSelected = true },
            new() { Id = "npm_cache", Title = "Pamięć podręczna pakietów npm", Category = "Programowanie", Description = "Pakiety pobrane przez menedżer npm.", SizeBytes = 140 * 1024 * 1024, CanClean = true, IsSelected = false },
            new() { Id = "empty-fixture", Title = "Pusta kategoria", Category = "System", CanClean = true, IsSelected = false },
            new() { Id = "error-fixture", Title = "Niedostępna lokalizacja", Category = "System", Status = "Błąd odczytu: brak dostępu (przykład testowy).", CanClean = true, IsSelected = false }
        };
        int count = 0;
        void Capture(string state)
        {
            foreach (int width in new[] { 1180, 1366 })
            {
                Render(root, output, $"{(light ? "light" : "dark")}-cleaner-{state}-{width}", width, 820, 1);
                count++;
            }
        }
        try
        {
            view.SetItems(fixture);
            view.SetScanState(true); view.SetScanState(false); Capture("idle");
            view.SetScanState(true);
            fixture[0].IsScanned = true; fixture[1].IsScanned = true; fixture[2].IsScanning = true;
            Capture("scanning");
            fixture[2].IsScanning = false;
            foreach (var item in fixture.Take(5)) { item.IsScanned = true; item.Status = "Odczyt zakończony (test interfejsu)."; }
            view.SetScanCompleted(); Capture("results");
            fixture[1].IsSelected = true;
            Layout(root, 1180, 820);
            var details = Descendants(view).OfType<ToggleButton>().First(t => t.Name == "DetailsToggle" && ReferenceEquals(t.DataContext, fixture[0]));
            details.IsChecked = true; Capture("details");
            view.SetCleaningState(true); Capture("cleaning");
            view.SetResult(680 * 1024 * 1024, 120, 0); Capture("done");
            view.SetResult(320 * 1024 * 1024, 83, 2); Capture("partial-outcome");
            view.SetScanState(true);
            foreach (var item in fixture) { item.SizeBytes = 0; item.IsScanned = true; item.Status = "Brak danych (test interfejsu)."; }
            view.SetScanCompleted(); Capture("empty-results");
        }
        finally { view.SetItems(window.CleanItems); view.SetScanState(true); view.SetScanState(false); }
        return count;
    }

    private static void BackToTools(MainWindow window)
    {
        var detail = (FrameworkElement)window.FindName("ToolDetailPanel");
        Descendants(detail).OfType<Button>().Single(b => Equals(b.Content, "← Narzędzia")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private static Button[] VisibleButtons(FrameworkElement root) => Descendants(root).OfType<Button>().Where(b => VisibleWithin(b, root)).ToArray();

    private static bool VisibleWithin(DependencyObject child, DependencyObject root)
    {
        for (DependencyObject? current = child; current != null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is UIElement element && element.Visibility != Visibility.Visible) return false;
            if (ReferenceEquals(current, root)) return true;
        }
        return false;
    }

    private static void Layout(FrameworkElement root, int width, int height)
    {
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
    }

    private static void VerifyExplorer(MainWindow window, FrameworkElement root)
    {
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        var switchView = typeof(MainWindow).GetMethod("SwitchView", BindingFlags.NonPublic | BindingFlags.Instance)!;
        switchView.Invoke(window, new object[] { 2 });
        root.Measure(new Size(1366, 900));
        root.Arrange(new Rect(0, 0, 1366, 900));
        root.UpdateLayout();
        var vm = (ExplorerViewModel)typeof(MainWindow).GetField("_explorerViewModel", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
        var buttons = Descendants(root).OfType<Button>().Where(b => b.DataContext is DriveModel && b.Tag is string && VisibleWithin(b, root)).ToArray();
        Check(buttons.Length == window.Drives.Count && buttons.Length > 0, "Every ready fixed volume has a real XAML button");
        foreach (var button in buttons)
        {
            var drive = (DriveModel)button.DataContext;
            Check((string)button.Tag == drive.RootPath, "Volume button binds normalized root");
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            PumpUntil(() => !vm.IsBusy);
            Check(vm.CurrentPath == drive.RootPath, "Click selects " + drive.RootPath);
            Check(((TextBox)window.FindName("CurrentPathTextBox")).Text == drive.RootPath, "Visible path changes after click");
            Check(((TextBlock)window.FindName("ActiveVolumeText")).Text.Contains(drive.RootPath), "Active volume label changes");
            Check(vm.Items.All(i => string.Equals(Path.GetDirectoryName(i.Path.TrimEnd('\\')), drive.RootPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetDirectoryName(i.Path.TrimEnd('\\')), drive.RootPath, StringComparison.OrdinalIgnoreCase)), "Rows belong to selected root");
            Check(ReferenceEquals(window.ExplorerItems, vm.Items), "MainWindow binds the active result collection");
            Console.WriteLine($"PASS actual volume click {drive.RootPath}: {vm.Items.Count} current rows");
        }
        foreach (var button in buttons.Reverse()) button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        PumpUntil(() => !vm.IsBusy);
        Check(vm.CurrentPath == ((DriveModel)buttons[0].DataContext).RootPath, "Rapid clicks preserve final selection");
        var artifacts = Path.GetFullPath(".artifacts");
        var sandbox = Path.Combine(artifacts, "explorer-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sandbox);
        try
        {
            var populated = Directory.CreateDirectory(Path.Combine(sandbox, "populated")).FullName;
            var empty = Directory.CreateDirectory(Path.Combine(sandbox, "empty")).FullName;
            File.WriteAllText(Path.Combine(populated, "owned-test-file.txt"), "test");
            Wait(vm.NavigateAsync(populated));
            Check(vm.Items.Count == 1 && vm.Items[0].Name == "owned-test-file.txt", "Known folder contents load");
            Wait(vm.NavigateAsync(empty));
            Check(vm.Items.Count == 0 && vm.Status.Contains("Brak elementów"), "Empty folder clears previous rows");
            Wait(vm.NavigateAsync(populated));
            Wait(vm.NavigateAsync(Path.Combine(sandbox, "missing")));
            Check(vm.Items.Count == 0 && vm.Status.StartsWith("Nie można"), "Missing folder clears stale data and reports error");
            var pending = vm.NavigateAsync(populated);
            vm.Cancel();
            Wait(pending);
            Check(vm.Items.Count == 0 && vm.Status.Contains("anulowany") && !vm.IsBusy, "Cancel discards pending result");
            var first = vm.NavigateAsync(populated);
            var second = vm.NavigateAsync(empty);
            Wait(Task.WhenAll(first, second));
            Check(vm.CurrentPath == empty && vm.Items.Count == 0 && !vm.IsBusy, "Older response cannot replace newer empty folder");
            Wait(vm.NavigateAsync(populated));
            Check(vm.Items.Count == 1 && !vm.IsBusy, "Recovery after errors and cancellation");
            foreach (var form in new[] { "d", "d:", @"d:\", "d:/" })
                Check(DriveModel.NormalizePath(form) == @"D:\", "Drive normalization: " + form);
            bool rejected = false;
            try { DriveModel.NormalizePath("relative/folder"); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Relative path rejected");
            Console.WriteLine("PASS explorer empty/missing/cancel/retry/stale-response and root normalization checks.");
        }
        finally
        {
            // Only the unique directory created by this test is removed.
            var resolved = Path.GetFullPath(sandbox);
            if (!resolved.StartsWith(artifacts + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(resolved).StartsWith("explorer-test-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unsafe test cleanup path");
            Directory.Delete(resolved, recursive: true);
        }
        // Leave the disk render showing a real volume instead of test fixtures.
        buttons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        PumpUntil(() => !vm.IsBusy);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Wait(Task task)
    {
        PumpUntil(() => task.IsCompleted);
        task.GetAwaiter().GetResult();
    }

    private static void PumpUntil(Func<bool> completed)
    {
        var timer = Stopwatch.StartNew();
        while (!completed())
        {
            if (timer.Elapsed > TimeSpan.FromSeconds(60)) throw new TimeoutException("WPF operation did not finish");
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
            Thread.Sleep(1);
        }
        // Flush binding and handler continuations without starting the window.
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static void Render(FrameworkElement root, string output, string name, int width, int height, int scale)
    {
        Layout(root, width, height);
        var bitmap = new RenderTargetBitmap(width * scale, height * scale, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(output, name + ".png"));
        encoder.Save(file);
    }
}
