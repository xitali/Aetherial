# Aetherial — Storage & Diagnostics

Natywna aplikacja Windows 10/11 x64 w C# / .NET 8 / WPF. Interfejs po polsku,
ciemny i jasny motyw, przegląd dysków, czyszczenie, eksplorator plików,
inwentaryzacja PnP, diagnostyka, katalog aplikacji oraz narzędzia systemowe.

## Dane i ograniczenia

Woluminy pochodzą z Windows, urządzenia i sterowniki z CIM/PnP oraz Windows Update.
Stan instalacji aplikacji jest sprawdzany lokalnie; katalog pakietów jest kuratorowaną
listą identyfikatorów winget. Brak odczytu nie jest wynikiem poprawnym.
Aplikacja nie deklaruje procentowego zdrowia SSD ani temperatur bez źródła pomiaru.
Część funkcji wymaga administratora, internetu, winget lub obsługi polecenia przez Windows.
Nie uruchamiaj równocześnie aplikacji korzystających z folderu przenoszonego dowiązaniem.

## Uruchomienie i wydanie

Wymagany .NET 8 SDK na Windows:

```powershell
dotnet build DiskOptimizer.csproj -c Release
dotnet run --project tests/Aetherial.Regression/Aetherial.Regression.csproj -c Release
./scripts/release.ps1
```

Gotowy samodzielny plik EXE i instrukcja znajdują się w `release/latest`.
Nie jest to Native AOT: jest to samodzielny pakiet .NET WPF single-file.
Skrypt testuje i publikuje do stagingu przed zastąpieniem poprzedniego wydania.
Wersja pochodzi z `DiskOptimizer.csproj`.

## GitHub

Repozytorium zawiera kod, zasoby, testy, instrukcję i dokumentację zmian.
Nie dodawaj EXE/DLL/PDB, bin/obj/publish/release, ustawień użytkownika, logów ani raportów.
Workflow `.github/workflows/release.yml` buduje i testuje zmiany. Tag `v<wersja>`
publikuje ZIP jako GitHub Release, a po powodzeniu usuwa wcześniejsze opublikowane
release (historia Git i tagi pozostają). Publikacja wymaga uprawnień GitHub.

## Struktura

- `App.xaml`: wspólne zasoby i style.
- `MainWindow.xaml`: układ oraz widoki.
- `MainWindow.xaml.cs`: nawigacja i koordynacja operacji.
- `Models/`, `Services/`: modele, odczyty Windows i operacje.
- `tests/`: izolowane regresje i kontrola renderowania.
- `scripts/`: powtarzalne wydanie.
- `docs/`: plan, zmiany i wynik weryfikacji.

Instrukcja: [DiskOptimizer_Manual.html](DiskOptimizer_Manual.html).
Plan: [docs/PLAN_WDROZENIA.md](docs/PLAN_WDROZENIA.md).
Licencja: [MIT](LICENSE).
