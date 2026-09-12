using System.IO;
using System.Windows;
using System.Windows.Media;

namespace DiskOptimizer;

public partial class App : Application
{
    public static void ApplyTheme(bool light)
    {
        var palette = new Dictionary<string, string>
        {
            ["BgDarkBrush"] = light ? "#F3F5F9" : "#0C1018",
            ["CardBgBrush"] = light ? "#FFFFFF" : "#131A26",
            ["CardHoverBgBrush"] = light ? "#E9EEF6" : "#1B2636",
            ["CardBorderBrush"] = light ? "#CED7E5" : "#293548",
            ["AccentBrush"] = light ? "#4F46E5" : "#818CF8",
            ["AccentHoverBrush"] = light ? "#4338CA" : "#A5B4FC",
            ["TextPrimaryBrush"] = light ? "#172033" : "#EEF2FA",
            ["TextSecondaryBrush"] = light ? "#526078" : "#A8B4C8",
            ["SuccessBrush"] = light ? "#167448" : "#6EE7B7",
            ["WarningBrush"] = light ? "#915B08" : "#FCD34D",
            ["ErrorBrush"] = light ? "#B42338" : "#FDA4AF",
            ["InfoBrush"] = light ? "#176999" : "#7DD3FC",
            ["AccentSoftBrush"] = light ? "#EAE9FF" : "#202745",
            ["OnAccentBrush"] = light ? "#FFFFFF" : "#101526"
        };
        foreach (var (key, value) in palette)
            Current.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) => WriteCrash(args.ExceptionObject.ToString());
        TaskScheduler.UnobservedTaskException += (_, args) => { WriteCrash(args.Exception.ToString()); args.SetObserved(); };
        DispatcherUnhandledException += (_, args) =>
        {
            WriteCrash(args.Exception.ToString());
            MessageBox.Show($"Nie ukończono operacji:\n{args.Exception.Message}\n\nDziennik: {CrashPath}", "Aetherial — błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };
        base.OnStartup(e);
    }

    private static string CrashPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aetherial", "crash.log");
    private static void WriteCrash(string? message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CrashPath)!);
            if (File.Exists(CrashPath) && new FileInfo(CrashPath).Length > 2_000_000) File.WriteAllText(CrashPath, "");
            File.AppendAllText(CrashPath, $"[{DateTime.Now:O}] {message}{Environment.NewLine}");
        }
        catch { }
    }
}
