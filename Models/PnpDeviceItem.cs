using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DiskOptimizer.Models;

public class PnpDeviceItem : INotifyPropertyChanged
{
    private string _status = "Sprawny i aktywny";
    private string _statusColor = "#81C784";
    private bool _needsUpdate = false;
    private string _updateStatusBadge = "✓ Zainstalowany (Aktualny)";
    private string _updateBadgeColor = "#81C784";
    private string _updateBadgeBg = "#143820";

    public string Category { get; set; } = "Inne";
    public string CategoryIcon { get; set; } = "🔌";
    public string DeviceName { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string DriverVersion { get; set; } = "Domyślny systemowy";
    public string DriverDate { get; set; } = "";
    public string UpdateMethod { get; set; } = "Windows Update WHQL";
    public string ActionButtonText { get; set; } = "Szczegóły";
    public string ActionTarget { get; set; } = "";
    public string NewVersionAvailable { get; set; } = "";

    public bool NeedsUpdate
    {
        get => _needsUpdate;
        set { _needsUpdate = value; OnPropertyChanged(); }
    }

    public string UpdateStatusBadge
    {
        get => _updateStatusBadge;
        set { _updateStatusBadge = value; OnPropertyChanged(); }
    }

    public string UpdateBadgeColor
    {
        get => _updateBadgeColor;
        set { _updateBadgeColor = value; OnPropertyChanged(); }
    }

    public string UpdateBadgeBg
    {
        get => _updateBadgeBg;
        set { _updateBadgeBg = value; OnPropertyChanged(); }
    }

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public string StatusColor
    {
        get => _statusColor;
        set { _statusColor = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
