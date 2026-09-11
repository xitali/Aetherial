using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DiskOptimizer.Models;

public enum CleanActionType
{
    RecycleBin,
    DeleteFiles,
    Command,
    SpecialAction,
    Informational
}

public class CleanItem : INotifyPropertyChanged
{
    private bool _isSelected = true;
    private bool _isScanning;
    private bool _isScanned;
    private bool _isCleaning;
    private long _sizeBytes;
    private int _itemCount;
    private string _status = "Oczekuje na skanowanie";

    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Icon { get; set; } = "📁";
    public string Path { get; set; } = string.Empty;
    public CleanActionType ActionType { get; set; } = CleanActionType.DeleteFiles;
    public string? Command { get; set; }
    public string? CommandArgs { get; set; }
    public bool CanClean { get; set; } = true;
    public string BadgeText { get; set; } = "Bezpieczne";
    public string BadgeColor { get; set; } = "#2E7D32"; // Dark green

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsScanning
    {
        get => _isScanning;
        set
        {
            if (_isScanning != value)
            {
                _isScanning = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsScanned
    {
        get => _isScanned;
        set
        {
            if (_isScanned != value)
            {
                _isScanned = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsCleaning
    {
        get => _isCleaning;
        set
        {
            if (_isCleaning != value)
            {
                _isCleaning = value;
                OnPropertyChanged();
            }
        }
    }

    public long SizeBytes
    {
        get => _sizeBytes;
        set
        {
            if (_sizeBytes != value)
            {
                _sizeBytes = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FormattedSize));
            }
        }
    }

    public int ItemCount
    {
        get => _itemCount;
        set
        {
            if (_itemCount != value)
            {
                _itemCount = value;
                OnPropertyChanged();
            }
        }
    }

    public string Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
            }
        }
    }

    public string FormattedSize => DriveModel.FormatBytes(SizeBytes);

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
