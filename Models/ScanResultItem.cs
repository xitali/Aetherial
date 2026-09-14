using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DiskOptimizer.Models;

public enum Severity
{
    Critical,
    Warning,
    Info
}

public enum FixAction
{
    CleanFiles,
    TrimSsd,
    OptimizeRam,
    UpdateDriver,
    DevCleanup,
    Generic
}

public class ScanResultItem : INotifyPropertyChanged
{
    private bool _isSelected;
    private string _status = "Wykryto";
    private string _statusColor = "#FBBF24";

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "Dysk";
    public Severity Severity { get; set; } = Severity.Warning;
    public long SizeBytes { get; set; }
    public FixAction Fix { get; set; } = FixAction.Generic;
    public string DetailPath { get; set; } = string.Empty;
    public bool CanFix { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
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

    public string FormattedSize => SizeBytes > 0 ? DriveModel.FormatBytes(SizeBytes) : "—";

    public string SeverityBadge => Severity switch
    {
        Severity.Critical => "KRYTYCZNY",
        Severity.Warning => "OSTRZEŻENIE",
        _ => "ZALECENIE"
    };

    public string SeverityColor => Severity switch
    {
        Severity.Critical => "#EF4444",
        Severity.Warning => "#F59E0B",
        _ => "#38BDF8"
    };

    public string SeverityBg => Severity switch
    {
        Severity.Critical => "#EF444420",
        Severity.Warning => "#F59E0B20",
        _ => "#38BDF820"
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
