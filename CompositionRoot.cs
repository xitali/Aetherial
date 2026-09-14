using System.IO;
using DiskOptimizer.Services;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace DiskOptimizer;

/// <summary>Application lifetime and service composition, independent of window construction.</summary>
public static class CompositionRoot
{
    public static string LogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aetherial", "logs");

    public static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        // Factory registration lets the container own and flush the logger on disposal.
        services.AddSingleton<ILogger>(_ => CreateLogger());
        services.AddSingleton<SettingsService>();
        services.AddSingleton<CustomTargetService>();
        services.AddSingleton<HistoryService>();
        services.AddSingleton<IHistoryService>(provider => provider.GetRequiredService<HistoryService>());
        services.AddSingleton<SystemRestoreService>();
        services.AddSingleton<ISystemRestoreService>(provider => provider.GetRequiredService<SystemRestoreService>());
        services.AddTransient<DiskScannerService>();
        services.AddTransient<DiskCleanerService>();
        services.AddTransient<CleanupWorkflowService>();
        services.AddSingleton<DriverCatalogService>();
        services.AddTransient<DriverUpdaterService>();
        services.AddTransient<FileSystemExplorerService>();
        services.AddTransient<MemoryOptimizerService>();
        services.AddTransient<SoftwareInstallerService>();
        services.AddTransient<SymlinkService>();
        services.AddTransient<CompactOsService>();
        services.AddTransient<DevProjectsService>();
        services.AddTransient<ScanService>();
        services.AddTransient<IScanService>(provider => provider.GetRequiredService<ScanService>());
        services.AddTransient<FixService>();
        services.AddTransient<IFixService>(provider => provider.GetRequiredService<FixService>());
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    private static ILogger CreateLogger()
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            return new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.WithProperty("Application", "Aetherial")
                .WriteTo.File(Path.Combine(LogDirectory, "aetherial-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    fileSizeLimitBytes: 5_000_000,
                    rollOnFileSizeLimit: true,
                    shared: true)
                .CreateLogger();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Diagnostics must not prevent startup when the profile directory is unavailable.
            System.Diagnostics.Trace.TraceWarning("Aetherial log unavailable: {0}", exception.Message);
            return new LoggerConfiguration().CreateLogger();
        }
    }
}
