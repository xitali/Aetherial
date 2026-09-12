using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
            foreach (var item in new DiskScannerService().GetDefaultItems()) window.CleanItems.Add(item);
            foreach (var item in new SoftwareInstallerService().GetCuratedCatalog()) window.AppsList.Add(item);
            foreach (var item in new SymlinkService().GetDefaultPresets()) window.SymlinkPresets.Add(item);
            var output = Path.GetFullPath(args.FirstOrDefault() ?? ".artifacts/visual");
            Directory.CreateDirectory(output);
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
                window.Drives.Add(new DriveModel { DriveLetter = drive.Name, VolumeLabel = drive.VolumeLabel, TotalBytes = drive.TotalSize, FreeBytes = drive.AvailableFreeSpace });

            var mem = MemoryOptimizerService.GetMemoryMetrics();
            ((TextBlock)window.FindName("RamStatusText")).Text = $"{mem.loadPercent}% • {DriveModel.FormatBytes((long)mem.availBytes)} dostępne";
            ((TextBlock)window.FindName("TopUptimeText")).Text = TimeSpan.FromMilliseconds(Environment.TickCount64).ToString(@"d\d\ hh\h\ mm\m");
            var hardware = new DriverUpdaterService().GetHardwareSpecsAsync().GetAwaiter().GetResult();
            ((TextBlock)window.FindName("DashboardCpuText")).Text = hardware.CpuName;
            ((TextBlock)window.FindName("DashboardGpuText")).Text = hardware.GpuPrimary;
            ((TextBlock)window.FindName("DashboardNetworkText")).Text = hardware.Network;
            ((TextBlock)window.FindName("HardwareSummaryText")).Text = hardware.Motherboard + " • " + hardware.Ram;
            foreach (var device in new DriverUpdaterService().GetConnectedPnpDevicesAsync().GetAwaiter().GetResult()) window.PnpDevices.Add(device);
            ((TextBlock)window.FindName("HardwareInventorySummaryText")).Text = $"{window.PnpDevices.Count} urządzeń z Windows CIM";
            window.ScanResultItems.Add(new ScanResultItem { Name = "Tymczasowe pliki systemowe", Description = "Kosz i cache Windows", SizeBytes = 1024 * 1024 * 350, Severity = Severity.Warning });
            window.HistoryItems.Add(new HistoryEntry { OperationType = "Czyszczenie dysku", BytesSaved = 1024 * 1024 * 500, ItemsFixed = 3, Success = true, Summary = "Usunięto zbędne pliki cache" });
            var views = new[] { "ViewDashboard", "ViewCleaner", "ViewExplorer", "ViewDrivers", "ViewTools", "ViewApps", "ViewSettings", "ViewScanner", "ViewHistory" };
            int count = 0;
            foreach (bool light in new[] { false, true })
            {
                App.ApplyTheme(light);
                foreach (var view in views)
                {
                    foreach (var name in views) ((FrameworkElement)window.FindName(name)).Visibility = name == view ? Visibility.Visible : Visibility.Collapsed;
                    Render(root, output, $"{(light ? "light" : "dark")}-{view}-1366", 1366, 900, 1);
                    count++;
                }
                ((FrameworkElement)window.FindName("ViewSettings")).Visibility = Visibility.Collapsed;
                ((FrameworkElement)window.FindName("ViewDashboard")).Visibility = Visibility.Visible;
                Render(root, output, $"{(light ? "light" : "dark")}-dashboard-1440p", 2560, 1440, 1);
                Render(root, output, $"{(light ? "light" : "dark")}-dashboard-4k-200dpi", 1920, 1080, 2);
                count += 2;
            }
            Console.WriteLine($"PASS {count} WPF renders. Actual local hardware reads, no startup or destructive handlers invoked.");
            // Confirm keyboard navigation routes all seven views without loading data or invoking actions.
            var switchView = typeof(MainWindow).GetMethod("SwitchView", BindingFlags.NonPublic | BindingFlags.Instance)!;
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
