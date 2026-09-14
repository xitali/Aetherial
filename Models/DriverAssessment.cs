namespace DiskOptimizer.Models;

public enum DriverAssessmentStatus { Unknown, UpToDate, UpdateAvailable, NewerInstalled, Unsupported, Error }

/// <summary>Evidence from a manufacturer catalogue, separate from PnP device health and Windows Update.</summary>
public sealed record DriverAssessment
{
    public string DeviceId { get; init; } = "";
    public string DeviceName { get; init; } = "";
    public string InstalledVersion { get; init; } = "";
    public string LatestVersion { get; init; } = "";
    public string ReleaseDate { get; init; } = "";
    public string SourceName { get; init; } = "";
    public string SourceUrl { get; init; } = "";
    public string Detail { get; init; } = "";
    public DateTimeOffset CheckedAt { get; init; } = DateTimeOffset.Now;
    public DriverAssessmentStatus Status { get; init; } = DriverAssessmentStatus.Unknown;
    public bool HasUpdate => Status == DriverAssessmentStatus.UpdateAvailable;
    public bool CanOpenSource => Uri.TryCreate(SourceUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
    public string StatusText => Status switch
    {
        DriverAssessmentStatus.UpToDate => "Aktualny w sprawdzonym katalogu",
        DriverAssessmentStatus.UpdateAvailable => "Dostępna nowsza wersja",
        DriverAssessmentStatus.NewerInstalled => "Zainstalowana wersja nowsza od katalogu",
        DriverAssessmentStatus.Unsupported => "Weryfikacja u producenta",
        DriverAssessmentStatus.Error => "Nie udało się sprawdzić",
        _ => "Aktualność niepotwierdzona"
    };
}
