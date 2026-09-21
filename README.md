# Aetherial 6.3 — Storage & Diagnostics

Polska aplikacja Windows 10/11 x64 w C# / .NET 8 / WPF do przeglądania dysków,
czyszczenia wybranych danych i sprawdzania sprzętu. Pracuje na odczytach Windows;
brak pomiaru lub odpowiedzi producenta pozostaje wyraźnie oznaczony.

## Główne przepływy

- **Sprzęt:** odczyt identyfikatorów PnP i zainstalowanych wersji, następnie porównanie
  obsługiwanych kart NVIDIA z oficjalnym katalogiem Game Ready WHQL DCH. Lista pokazuje
  wersję lokalną, wersję katalogową, źródło, zgodność i czas sprawdzenia. Filtry pomagają
  oddzielić dostępne aktualizacje od urządzeń niezweryfikowanych.
- **Czyść:** skan → przegląd kategorii i rozmiarów → własny wybór → potwierdzenie →
  wynik. Pozycje zaczynają odznaczone. Operacja wymaga utworzenia punktu przywracania;
  nie zastępuje on kopii usuwanych plików. Raport rozróżnia zmierzone odzyskane miejsce,
  błędy/pominięcia i anulowanie.
- **Dyski:** wybór wykrytego woluminu zmienia ścieżkę i odczytywaną listę. Nowsza
  nawigacja anuluje poprzedni odczyt; spóźniony wynik nie zastępuje bieżącego folderu.
- **Historia:** trwały zapis wykonanych
  operacji. Licznik wyników nie jest procentową oceną zdrowia komputera.

## Nawigacja i preferencje

Jedno boczne menu udostępnia Czyść, Dyski, Sterowniki, Programy, Narzędzia, Historię i Ustawienia. Czyść jest ekranem startowym. Szczegóły pojawiają się po rozwinięciu, a operacje usuwania po zaznaczeniu. Narzędzia zaczynają od czterech kategorii z przyciskiem powrotu. Ustawienia mają trzy działy:
„Wygląd i odczyty”, „Programy i pliki” oraz „Windows i raporty”.

W preferencjach można zapisać sześć ustawień: motyw, włączenie cyklicznego odczytu
parametrów, jego interwał (5–120 sekund), skan sprzętu po otwarciu, skan cache
po otwarciu oraz próg dużych plików (100–10 240 MB). Motyw i timer są stosowane
w bieżącej sesji, a nowy próg przy następnym wyszukiwaniu. Automatyczny skan
wyłącznie odczytuje dane; czyszczenie i instalacja nadal wymagają własnego wyboru.

Programy pokazują rzeczywistą listę instalacji z WinGet, oddzielną listę aktualizacji oraz opcjonalny katalog do instalowania nowych aplikacji. Przy niedostępnym WinGet odczyt z rejestru pozostaje wyraźnie niezweryfikowany; nie pozwala na instalację z niepotwierdzonego ID. Aktualizacje dotyczą wybranych programów i respektują ustawienie cichej instalacji.

## Zakres i źródła danych

Automatyczne porównanie sterowników obejmuje obecnie obsługiwane karty NVIDIA na
Windows 10/11 x64. AMD, Intel i pozostałe urządzenia mają status niezweryfikowany
oraz dostępne odnośniki do producentów. Aplikacja nie instaluje automatycznie
sterowników z tego katalogu. Windows Update pozostaje osobnym narzędziem zaawansowanym.
Szczegóły: [katalog sterowników](docs/DRIVER_CATALOG.md).

Woluminy, urządzenia i wersje pochodzą z systemu. Identyfikatory winget, adresy
producentów i reguły lokalizacji cache to dane referencyjne, a nie pomiary komputera.
Nie wszystkie niestandardowe lokalizacje programów są wykrywane. Nie ma pomiarów
temperatur ani pełnej diagnostyki zużycia SSD. Wybrane operacje wymagają administratora,
internetu, winget lub aktywnej ochrony systemu Windows.

## Budowanie i wydanie

Wymagany .NET 8 SDK na Windows:

```powershell
dotnet build Aetherial.sln -c Release
dotnet run --project tests/Aetherial.Regression -c Release
dotnet run --project tests/Aetherial.DriverTests -c Release
dotnet run --project tests/Aetherial.ScanTests -c Release
./scripts/release.ps1
```

Skrypt wykonuje testy offline i zdarzeń WPF, publikuje do stagingu, sprawdza EXE i zapisuje SHA-256,
a następnie zastępuje `release/latest`. Dotychczasowy pakiet pozostaje do czasu
powodzenia publikacji. Wyniki kompilacji znanych projektów są sprzątane po wydaniu.
Pakiet jest samodzielnym .NET WPF single-file, nie Native AOT.
Test katalogu z `-- --live` jest opcjonalny i zależy od dostępności usługi NVIDIA.

## Lokalna instalacja

Po poprawnym przygotowaniu `release/latest` zamknij zainstalowaną aplikację i uruchom:

```powershell
./scripts/install-local.ps1
```

Skrypt sprawdza SHA-256 EXE przed kopiowaniem i po nim, instaluje pakiet w
`%LocalAppData%\Programs\Aetherial`, tworzy skróty na pulpicie i w menu Start oraz
wpis deinstalacji bieżącego użytkownika. Nie pobiera wydania z internetu.
Usuwanie zainstalowanej aplikacji jest dostępne przez wpis Windows albo:

```powershell
./scripts/install-local.ps1 -Uninstall
```

Deinstalacja zachowuje ustawienia i historię użytkownika. Skrypt lokalny nie jest
podpisanym instalatorem MSIX/EXE i nie zapewnia automatycznych aktualizacji.

## Architektura

- `Aetherial.Core`: modele i kontrakty; bez zależności od WPF.
- `Aetherial.Services`: odczyty Windows, katalog sterowników, czyszczenie i historia.
- `DiskOptimizer.csproj`: aplikacja WPF; `CompositionRoot.cs` konfiguruje DI.
- `Views/` i `ViewModels/`: nowe widoki sprzętu i czyszczenia oraz stan eksploratora,
  z użyciem CommunityToolkit.Mvvm. Starsze narzędzia nadal częściowo korzystają z code-behind.
- `Models/`, `Services/`: pliki źródłowe dołączane do odpowiednich projektów bibliotek.
- `tests/`: izolowane regresje, sprawdzanie katalogu, skanera i renderowania WPF.

Historia, ustawienia i rotowane logi Serilog trafiają do profilu użytkownika w
`%LocalAppData%\Aetherial`, nie do repozytorium.

## GitHub i dalsze prace

Git zawiera kod, zasoby, testy i dokumentację. EXE/DLL/PDB, bin/obj, paczki, logi
i lokalne rendery są ignorowane. Tag `v<wersja>` uruchamia workflow publikujący ZIP;
starsze opublikowane releases są usuwane dopiero po udanym przesłaniu nowego.
Historia Git i tagi pozostają. Wersja produktu pochodzi z `DiskOptimizer.csproj`.

Plan użytkownika: [AETHERIAL_PLAN.md](AETHERIAL_PLAN.md).
Stan modułów: [AUDIT.md](AUDIT.md). Pozostałe prace: [BACKLOG.md](BACKLOG.md).
Weryfikacja: [docs/WERYFIKACJA.md](docs/WERYFIKACJA.md). Licencja: [MIT](LICENSE).
