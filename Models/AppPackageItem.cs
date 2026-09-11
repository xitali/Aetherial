using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DiskOptimizer.Models;

public class AppPackageItem : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isInstalled;
    private bool _hasUpdate;
    private string _installedVersion = "";
    private string _availableVersion = "";
    private string _status = "Niezainstalowany";
    private string _statusColor = "#888898";
    private bool _isBusy;

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "📦";

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public bool IsInstalled
    {
        get => _isInstalled;
        set 
        { 
            _isInstalled = value; 
            OnPropertyChanged(); 
            OnPropertyChanged(nameof(ActionButtonText));
            OnPropertyChanged(nameof(ActionButtonVisible));
        }
    }

    public bool HasUpdate
    {
        get => _hasUpdate;
        set 
        { 
            _hasUpdate = value; 
            OnPropertyChanged(); 
            OnPropertyChanged(nameof(ActionButtonText));
            OnPropertyChanged(nameof(ActionButtonVisible));
        }
    }

    public string InstalledVersion
    {
        get => _installedVersion;
        set { _installedVersion = value; OnPropertyChanged(); }
    }

    public string AvailableVersion
    {
        get => _availableVersion;
        set { _availableVersion = value; OnPropertyChanged(); }
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

    public bool IsBusy
    {
        get => _isBusy;
        set 
        { 
            _isBusy = value; 
            OnPropertyChanged(); 
            OnPropertyChanged(nameof(CanAction));
        }
    }

    public bool CanAction => !_isBusy;

    public string ActionButtonText
    {
        get
        {
            if (HasUpdate) return "⬆️ Aktualizuj";
            if (IsInstalled) return "✓ Zainstalowano";
            return "⬇️ Zainstaluj";
        }
    }

    public bool ActionButtonVisible => !IsInstalled || HasUpdate;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
