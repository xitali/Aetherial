namespace DiskOptimizer.Models;

public class AppSettings
{
    public string DefaultInstallFolder { get; set; } = @"D:\Programy";
    public bool SilentInstall { get; set; } = true;
    public bool AutoCheckUpdates { get; set; } = true;
    public bool AutoCleanTempOnLaunch { get; set; } = false;
    public bool RecycleBinDefault { get; set; } = true;
}
