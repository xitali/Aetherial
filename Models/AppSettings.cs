namespace DiskOptimizer.Models;

public class AppSettings
{
    public string DefaultInstallFolder { get; set; } = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs");
    public string Theme { get; set; } = "Dark";
    public bool SilentInstall { get; set; } = true;
    public bool AutoCheckUpdates { get; set; } = true;
    public bool AutoCleanTempOnLaunch { get; set; } = false;
    public bool RecycleBinDefault { get; set; } = true;
}
