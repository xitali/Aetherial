using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DiskOptimizer.Models;

public class DiagnosticItem : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _category = "Hardware";
    private string _badgeText = string.Empty;
    private string _badgeForeground = "#F87171";
    private string _badgeBackground = "#EF444420";
    private string _borderBrush = "#EF4444";
    private string _title = string.Empty;
    private string _description = string.Empty;
    private string _actionButtonText = string.Empty;
    private string _actionTarget = string.Empty;
    private string _actionButtonStyle = "SecondaryButtonStyle";
    private string _noteText = string.Empty;
    private bool _isProblem = true;

    public string Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    public string Category
    {
        get => _category;
        set { _category = value; OnPropertyChanged(); }
    }

    public string BadgeText
    {
        get => _badgeText;
        set { _badgeText = value; OnPropertyChanged(); }
    }

    public string BadgeForeground
    {
        get => _badgeForeground;
        set { _badgeForeground = value; OnPropertyChanged(); }
    }

    public string BadgeBackground
    {
        get => _badgeBackground;
        set { _badgeBackground = value; OnPropertyChanged(); }
    }

    public string BorderBrush
    {
        get => _borderBrush;
        set { _borderBrush = value; OnPropertyChanged(); }
    }

    public string Title
    {
        get => _title;
        set { _title = value; OnPropertyChanged(); }
    }

    public string Description
    {
        get => _description;
        set { _description = value; OnPropertyChanged(); }
    }

    public string ActionButtonText
    {
        get => _actionButtonText;
        set { _actionButtonText = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasButton)); OnPropertyChanged(nameof(HasNote)); }
    }

    public string ActionTarget
    {
        get => _actionTarget;
        set { _actionTarget = value; OnPropertyChanged(); }
    }

    public string ActionButtonStyle
    {
        get => _actionButtonStyle;
        set { _actionButtonStyle = value; OnPropertyChanged(); }
    }

    public string NoteText
    {
        get => _noteText;
        set { _noteText = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasNote)); }
    }

    public bool HasButton => !string.IsNullOrEmpty(ActionButtonText);
    public bool HasNote => !string.IsNullOrEmpty(NoteText) && string.IsNullOrEmpty(ActionButtonText);

    public bool IsProblem
    {
        get => _isProblem;
        set { _isProblem = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
