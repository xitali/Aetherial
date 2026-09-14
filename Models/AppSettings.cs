namespace DiskOptimizer.Models;

public class AppSettings
{
    public string DefaultInstallFolder { get; set; } = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs");
    public string Theme { get; set; } = "Dark";
    public bool SilentInstall { get; set; } = true;
    public bool AutoCheckUpdates { get; set; } = true;
    public bool AutoCleanTempOnLaunch { get; set; } = false;
    public bool RecycleBinDefault { get; set; } = true;
    public bool AutoRefreshMetrics { get; set; } = true;
    public int MetricsRefreshSeconds { get; set; } = 15;
    public bool AutoScanHardwareOnOpen { get; set; } = true;
    public bool AutoScanCleanerOnOpen { get; set; } = false;
    public int LargeFileThresholdMb { get; set; } = 500;

    public void Validate()
    {
        if (Theme is not ("Dark" or "Light")) throw new ArgumentException("Wybierz motyw ciemny lub jasny.");
        if (MetricsRefreshSeconds is < 5 or > 120) throw new ArgumentException("Częstotliwość odświeżania musi wynosić od 5 do 120 sekund.");
        if (LargeFileThresholdMb is < 100 or > 10240) throw new ArgumentException("Próg dużych plików musi wynosić od 100 do 10 240 MB.");
        if (string.IsNullOrWhiteSpace(DefaultInstallFolder) || !System.IO.Path.IsPathFullyQualified(DefaultInstallFolder))
            throw new ArgumentException("Folder instalacji musi być pełną ścieżką, np. D:\\Programy.");
        _ = System.IO.Path.GetFullPath(DefaultInstallFolder);
    }
}
