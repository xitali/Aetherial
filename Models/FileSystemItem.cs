using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DiskOptimizer.Models;

public class FileSystemItem : INotifyPropertyChanged
{
    private bool _isSelected;
    private long _sizeBytes;
    private int _fileCount;
    private string _status = "Gotowy";

    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ParentDirectory { get; set; } = string.Empty;
    public string DriveLetter { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public DateTime LastModified { get; set; }
    public string RelativeAgeText { get; set; } = string.Empty;
    public string AgeBadgeColor { get; set; } = "#81C784";
    public string AgeBadgeBackground { get; set; } = "#1E3324";
    public string Extension { get; set; } = string.Empty;

    // Bezpieczeństwo systemu Windows
    public bool IsSystemProtected { get; set; }
    public bool CanDelete => !IsSystemProtected;

    public string TypeIcon => IsSystemProtected 
        ? "🔒" 
        : (IsDirectory ? "📁" : GetFileIcon(Extension));

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (CanDelete)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public long SizeBytes
    {
        get => _sizeBytes;
        set
        {
            _sizeBytes = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FormattedSize));
        }
    }

    public int FileCount
    {
        get => _fileCount;
        set { _fileCount = value; OnPropertyChanged(); }
    }

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public string FormattedSize => DriveModel.FormatBytes(SizeBytes);
    public string FormattedDate => LastModified > DateTime.MinValue 
        ? LastModified.ToString("yyyy-MM-dd HH:mm") 
        : "Nieznana";

    private static string GetFileIcon(string ext) => ext.ToLowerInvariant() switch
    {
        ".exe" or ".msi" => "⚙️",
        ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "📦",
        ".iso" or ".img" or ".vhdx" => "💿",
        ".mp4" or ".mkv" or ".avi" or ".mov" => "🎥",
        ".mp3" or ".wav" or ".flac" => "🎵",
        ".jpg" or ".png" or ".webp" or ".gif" => "🖼️",
        ".pdf" or ".doc" or ".docx" or ".txt" => "📄",
        ".dmp" or ".log" or ".tmp" or ".bak" => "🗑️",
        ".py" or ".cs" or ".js" or ".ts" or ".html" or ".json" => "💻",
        _ => "📄"
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
