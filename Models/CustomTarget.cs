using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace DiskOptimizer.Models;

public enum CustomDeleteMode
{
    ContentsOnly,
    EntireTarget
}

public class CustomTarget : INotifyPropertyChanged
{
    private bool _isSelected = true;
    private long _sizeBytes;
    private int _fileCount;
    private string _status = "Gotowy";
    private CustomDeleteMode _deleteMode = CustomDeleteMode.ContentsOnly;
    private int _daysOlderThan = 0;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Path { get; set; } = string.Empty;
    public bool IsDirectory { get; set; } = true;

    public string Name => !string.IsNullOrEmpty(Path) 
        ? System.IO.Path.GetFileName(Path.TrimEnd('\\', '/')) 
        : "Nieznany";

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public CustomDeleteMode DeleteMode
    {
        get => _deleteMode;
        set
        {
            _deleteMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FormattedMode));
        }
    }

    public int DaysOlderThan
    {
        get => _daysOlderThan;
        set
        {
            _daysOlderThan = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FormattedAge));
        }
    }

    [JsonIgnore]
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

    [JsonIgnore]
    public int FileCount
    {
        get => _fileCount;
        set { _fileCount = value; OnPropertyChanged(); }
    }

    [JsonIgnore]
    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    [JsonIgnore]
    public string FormattedSize => DriveModel.FormatBytes(SizeBytes);

    [JsonIgnore]
    public string FormattedMode => DeleteMode == CustomDeleteMode.ContentsOnly 
        ? "Tylko zawartość (folder zostaje)" 
        : "Cały folder / plik";

    [JsonIgnore]
    public string FormattedAge => DaysOlderThan > 0 
        ? $"> {DaysOlderThan} dni" 
        : "Wszystkie pliki";

    [JsonIgnore]
    public string TypeIcon => IsDirectory ? "📁" : "📄";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
