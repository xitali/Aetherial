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
    private bool _inventoryOnly;
    private bool _isPackageIdVerified;
    private bool _updatesChecked;

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "📦";
    public string Source { get; set; } = string.Empty;

    // Inventory records may only update after WinGet returns an exact package ID and source.
    // They must never become install targets when a subsequent scan is incomplete.
    public bool InventoryOnly
    {
        get => _inventoryOnly;
        set { _inventoryOnly = value; OnPropertyChanged(); NotifyActions(); }
    }

    public bool IsPackageIdVerified
    {
        get => _isPackageIdVerified;
        set { _isPackageIdVerified = value; OnPropertyChanged(); NotifyActions(); }
    }

    public bool UpdatesChecked
    {
        get => _updatesChecked;
        set { _updatesChecked = value; OnPropertyChanged(); }
    }

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
            NotifyActions();
        }
    }

    public bool HasUpdate
    {
        get => _hasUpdate;
        set 
        { 
            _hasUpdate = value; 
            OnPropertyChanged(); 
            NotifyActions();
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
        set { _availableVersion = value; OnPropertyChanged(); NotifyActions(); }
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
            NotifyActions();
        }
    }

    public bool CanInstall => !IsBusy && !InventoryOnly && IsPackageIdVerified && !IsInstalled;
    public bool CanUpdate => !IsBusy && IsInstalled && IsPackageIdVerified && HasUpdate && !string.IsNullOrWhiteSpace(AvailableVersion);
    public bool CanAction => CanInstall || CanUpdate;

    public string ActionButtonText
    {
        get
        {
            if (HasUpdate && IsPackageIdVerified) return "Aktualizuj";
            if (IsInstalled) return "✓ Zainstalowano";
            return "Zainstaluj";
        }
    }

    public bool ActionButtonVisible => IsPackageIdVerified && ((!InventoryOnly && !IsInstalled) || (IsInstalled && HasUpdate && !string.IsNullOrWhiteSpace(AvailableVersion)));

    private void NotifyActions()
    {
        OnPropertyChanged(nameof(CanInstall));
        OnPropertyChanged(nameof(CanUpdate));
        OnPropertyChanged(nameof(CanAction));
        OnPropertyChanged(nameof(ActionButtonVisible));
        OnPropertyChanged(nameof(ActionButtonText));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
