using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DiskOptimizer.ViewModels;
using DiskOptimizer;
using DiskOptimizer.Models;
using DiskOptimizer.Services;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            // Instantiate the actual WPF view without Show/Loaded or startup operations.
            var app = new App();
            app.InitializeComponent();
            var bindingErrors = new StringWriter();
            PresentationTraceSources.DataBindingSource.Listeners.Add(new TextWriterTraceListener(bindingErrors));
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
            var window = new MainWindow();
            var root = (FrameworkElement)window.Content;
            foreach (var item in new DiskScannerService().GetDefaultItems()) { item.IsSelected = false; window.CleanItems.Add(item); }
            foreach (var item in new SoftwareInstallerService().GetCuratedCatalog()) window.AppsList.Add(item);
            foreach (var item in new SymlinkService().GetDefaultPresets()) window.SymlinkPresets.Add(item);
            var output = Path.GetFullPath(args.FirstOrDefault(a => !a.StartsWith("--")) ?? ".artifacts/visual");
            Directory.CreateDirectory(output);
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
                window.Drives.Add(new DriveModel { DriveLetter = drive.Name, VolumeLabel = drive.VolumeLabel, TotalBytes = drive.TotalSize, FreeBytes = drive.AvailableFreeSpace });

            VerifyExplorer(window, root);
            VerifyCleaner();
            if (args.Contains("--interactions-only"))
            {
                PresentationTraceSources.DataBindingSource.Flush();
                if (bindingErrors.ToString().Contains("Error:")) throw new InvalidOperationException(bindingErrors.ToString());
                Console.WriteLine("PASS WPF binding error trace.");
                return 0;
            }
            SynchronizationContext.SetSynchronizationContext(null);
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
            window.ScanResultItems.Add(new ScanResultItem { Name = "Tymczasowe pliki systemowe", Description = "Kosz i cache Windows", SizeBytes = 1024 * 1024 * 350, Severity = Severity.Warning });
            window.HistoryItems.Add(new HistoryEntry { OperationType = "Czyszczenie dysku", BytesSaved = 1024 * 1024 * 500, ItemsFixed = 3, Success = true, Summary = "Usunięto zbędne pliki cache" });
            var views = new[] { "ViewDashboard", "ViewCleaner", "ViewExplorer", "ViewDrivers", "ViewTools", "ViewApps", "ViewSettings", "ViewScanner", "ViewHistory" };
            var switchView = typeof(MainWindow).GetMethod("SwitchView", BindingFlags.NonPublic | BindingFlags.Instance)!;
            int count = 0;
            foreach (bool light in new[] { false, true })
            {
                App.ApplyTheme(light);
                foreach (var view in views)
                {
                    switchView.Invoke(window, new object[] { Array.IndexOf(views, view) });
                    Render(root, output, $"{(light ? "light" : "dark")}-{view}-1366", 1366, 900, 1);
                    count++;
                    if (view is "ViewCleaner" or "ViewExplorer" or "ViewDrivers")
                    {
                        Render(root, output, $"{(light ? "light" : "dark")}-{view}-1180", 1180, 820, 1);
                        count++;
                    }
                }
                switchView.Invoke(window, new object[] { 0 });
                Render(root, output, $"{(light ? "light" : "dark")}-dashboard-1440p", 2560, 1440, 1);
                Render(root, output, $"{(light ? "light" : "dark")}-dashboard-4k-200dpi", 1920, 1080, 2);
                count += 2;
            }
            Console.WriteLine($"PASS {count} WPF renders. Actual local hardware reads, no startup or destructive handlers invoked.");
            // Confirm each navigation route selects only its own view.
            for (int i = 0; i < views.Length; i++)
            {
                switchView.Invoke(window, new object[] { i });
                if (views.Count(name => ((FrameworkElement)window.FindName(name)).Visibility == Visibility.Visible) != 1)
                    throw new InvalidOperationException("Navigation must select exactly one view");
            }
            Console.WriteLine($"PASS all {views.Length} navigation routes; Light/Dark resources load.");
            PresentationTraceSources.DataBindingSource.Flush();
            if (bindingErrors.ToString().Contains("Error:")) throw new InvalidOperationException(bindingErrors.ToString());
            Console.WriteLine("PASS WPF binding error trace.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void VerifyCleaner()
    {
        using var vm = new CleanerViewModel();
        var item = new CleanItem { Id = "test-only", Title = "Test", CanClean = true, IsSelected = true, SizeBytes = 4096 };
        vm.SetItems(new System.Collections.ObjectModel.ObservableCollection<CleanItem> { item });
        Check(!vm.CanClean, "Cleaner cannot run before a completed scan");
        vm.SetScanState(true);
        item.IsScanned = true;
        vm.SetScanCompleted();
        Check(vm.CanClean && vm.Progress == 100, "Completed selected scan enables review");
        vm.SetScanState(true);
        Check(!item.IsScanned && !vm.CanClean && vm.Progress == 0, "Rescan clears old completion and disables cleaning");
        vm.SetScanState(false);
        Check(!vm.CanClean && !item.IsScanned && vm.State == CleanerState.Idle, "Canceled rescan cannot reuse stale selection");
        vm.SetScanState(true);
        item.IsScanned = true;
        vm.SetScanState(false);
        Check(!vm.CanClean, "Partially completed canceled scan cannot authorize cleaning");
        Console.WriteLine("PASS cleaner completed/rescan/canceled/partial-scan state guards.");
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
        var buttons = Descendants(root).OfType<Button>().Where(b => b.DataContext is DriveModel && b.Tag is string).ToArray();
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
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width * scale, height * scale, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(output, name + ".png"));
        encoder.Save(file);
    }
}
