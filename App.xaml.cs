using System.Configuration;
using System.Data;
using System.Windows;

using System.IO;

namespace DiskOptimizer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            try
            {
                var msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] AppDomain Unhandled: {args.ExceptionObject}\n";
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), msg);
            }
            catch { }
        };

        DispatcherUnhandledException += (s, args) =>
        {
            try
            {
                var msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Dispatcher Unhandled: {args.Exception}\n";
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), msg);
                MessageBox.Show($"Wystąpił błąd działania aplikacji:\n\n{args.Exception.Message}\n\nSzczegóły zapisano w crash.log", 
                    "Aetherial Optimizer - Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                args.Handled = true;
            }
            catch { }
        };

        base.OnStartup(e);
    }
}


