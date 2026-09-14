using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DiskOptimizer.Models;

public class DriveModel : INotifyPropertyChanged
{
    private string _driveLetter = string.Empty;
    private string _volumeLabel = string.Empty;
    private long _totalBytes;
    private long _freeBytes;

    public string DriveLetter
    {
        get => _driveLetter;
        set { _driveLetter = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); OnPropertyChanged(nameof(RootPath)); }
    }

    public string RootPath => NormalizePath(DriveLetter);

    public static string NormalizePath(string input)
    {
        string path = input.Trim();
        if (System.Text.RegularExpressions.Regex.IsMatch(path, "^[A-Za-z]:?[\\\\/]?$"))
            return char.ToUpperInvariant(path[0]) + ":\\";
        if (!System.IO.Path.IsPathFullyQualified(path)) throw new ArgumentException("Podaj pełną ścieżkę folderu lub literę woluminu.");
        return System.IO.Path.GetFullPath(path);
    }

    public string VolumeLabel
    {
        get => _volumeLabel;
        set { _volumeLabel = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
    }

    public long TotalBytes
    {
        get => _totalBytes;
        set
        {
            _totalBytes = Math.Max(0, value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(UsedBytes));
            OnPropertyChanged(nameof(UsedPercent));
            OnPropertyChanged(nameof(FreePercent));
            OnPropertyChanged(nameof(FormattedTotal));
            OnPropertyChanged(nameof(FormattedUsed));
            OnPropertyChanged(nameof(FormattedFree));
            OnPropertyChanged(nameof(StatusColor));
        }
    }

    public long FreeBytes
    {
        get => _freeBytes;
        set
        {
            _freeBytes = Math.Max(0, value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(UsedBytes));
            OnPropertyChanged(nameof(UsedPercent));
            OnPropertyChanged(nameof(FreePercent));
            OnPropertyChanged(nameof(FormattedFree));
            OnPropertyChanged(nameof(FormattedUsed));
            OnPropertyChanged(nameof(StatusColor));
        }
    }

    public long UsedBytes => Math.Max(0, TotalBytes - FreeBytes);

    public double UsedPercent => TotalBytes > 0 ? Math.Round((double)UsedBytes / TotalBytes * 100, 1) : 0;
    public double FreePercent => TotalBytes > 0 ? Math.Clamp(Math.Round((double)FreeBytes / TotalBytes * 100, 1), 0, 100) : 0;

    public string FormattedTotal => FormatBytes(TotalBytes);
    public string FormattedUsed => FormatBytes(UsedBytes);
    public string FormattedFree => FormatBytes(FreeBytes);

    public string DisplayName => string.IsNullOrWhiteSpace(VolumeLabel) 
        ? $"Dysk ({DriveLetter})" 
        : $"{VolumeLabel} ({DriveLetter})";

    public string StatusColor => UsedPercent switch
    {
        > 90 => "#FF5252", // Czerwony
        > 75 => "#FFA726", // Pomarańczowy
        _ => "#4CAF50"    // Zielony
    };

    public static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
        int counter = 0;
        decimal number = Math.Max(0, bytes);
        while (number >= 1024 && counter < suffixes.Length - 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
