using System.Globalization;
using System.Windows.Data;
using DiskOptimizer.Models;

namespace DiskOptimizer.Views;

public sealed class CleanItemTitleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is CleanItem item ? item.Id switch
    {
        "recycle_bin" => "Kosz", "crash_dumps" => "Zrzuty awarii", "wer_reports" => "Raporty błędów Windows",
        "user_temp" => "Pliki tymczasowe", "windows_temp" => "Pliki tymczasowe Windows", "windows_update_cache" => "Pobrane aktualizacje Windows",
        "brave_cache" => "Cache Brave", "chrome_cache" => "Cache Chrome", "edge_cache" => "Cache Edge", "discord_cache" => "Cache Discord",
        "npm_cache" => "Cache npm", "uv_cache" => "Cache uv", "pip_cache" => "Cache pip", "nuget_cache" => "Pakiety NuGet",
        "huggingface_cache" => "Modele Hugging Face", "vscode_cache" => "Cache edytorów kodu", "nvidia_ota" => "Instalatory NVIDIA",
        "nvidia_dxcache" => "Shadery GPU", "thumbnail_cache" => "Miniatury Windows", "delivery_optimization" => "Cache aktualizacji sieciowych",
        _ => item.Title
    } : "";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class CleanItemWarningConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is CleanItem item ? item.Id switch
    {
        "recycle_bin" => "Opróżnienie Kosza trwale usunie znajdujące się w nim pliki.",
        "huggingface_cache" => "Modele zostaną usunięte. Ponowne użycie może wymagać pobrania wielu GB danych.",
        "nvidia_dxcache" => "Gry i aplikacje odbudują shadery; pierwsze uruchomienie może być wolniejsze.",
        "npm_cache" or "uv_cache" or "pip_cache" or "nuget_cache" => "Pakiety mogą wymagać ponownego pobrania. Zamknij działające instalacje i kompilacje.",
        "crash_dumps" or "wer_reports" => "Usuniesz dane przydatne przy diagnozowaniu awarii.",
        "windows_update_cache" or "delivery_optimization" => "Nie czyść w trakcie instalowania aktualizacji Windows.",
        _ => item.ActionType == CleanActionType.Command ? item.Description : ""
    } : "";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
