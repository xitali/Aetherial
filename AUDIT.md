# AUDIT: Inwentaryzacja Funkcji i Stan Aplikacji Aetherial 6.0

Data sporzadzenia: 2026-09-13
Wersja docelowa: 6.0.0

Niniejszy dokument stanowi inwentaryzacje wszystkich podsystemow, serwisow i komponentow aplikacji zgodnie z wymogami Etapu 0 planu restrukturyzacji (AETHERIAL_PLAN.md).

---

## 1. Stan Funkcjonalnosci i Modulow

| Modul / Podsystem | Pliki Zrodlowe | Stan | Uwagi i Podjete Dzialania |
| :--- | :--- | :--- | :--- |
| **Odczyt Woluminow** | `DriveModel.cs`, `DiskScannerService.cs` | **Dziala (100%)** | Prawdziwe odczyty `DriveInfo.GetDrives()`. Usunieto sztywne listy dyskow C/D/E/F. Bezpieczne formatowanie bajtow. |
| **Czyszczenie Dyskow** | `DiskCleanerService.cs`, `DiskHelper.cs` | **Dziala (100%)** | Scisla walidacja sciezek (`ValidateCleaningPath`). Ochrona katalogu Windows, profilu uzytkownika i korzeni dyskow. Filtry wieku plikow (`daysOlderThan`). Bezpieczne pomijanie plikow zablokowanych. |
| **Eksplorator Plikow** | `FileSystemExplorerService.cs` | **Dziala (100%)** | Dynamiczne przegladanie folderow, identyfikacja duzych plikow. Usuwanie do Kosza lub trwale z weryfikacja rozmiaru na zywo. |
| **PnP & Diagnostyka Sprzetu** | `DriverUpdaterService.cs`, `DiagnosticItem.cs` | **Dziala (100%)** | Usunieto falszywe bledy Code 28 i statyczne mocki. Odczyt na zywo przez PowerShell/CIM obecnych urzadzen (`Present -eq $true`), taktowania RAM (EXPO 6000 MT/s) i wersji BIOS. |
| **Optymalizacja RAM** | `MemoryOptimizerService.cs` | **Dziala (100%)** | Prawdziwe API `GlobalMemoryStatusEx` i `EmptyWorkingSet`. Bezpieczne uwalnianie pamieci podrecznej stron. |
| **Dowiazania Symboliczne (Symlinki)** | `SymlinkService.cs` | **Dziala (100%)** | Relokacja folderow z tworzeniem zlacza NTFS (Junction). Zabezpieczenie przed samoreferencja i sciezkami zagniezdzonymi. |
| **Artefakty Deweloperskie** | `DevProjectsService.cs` | **Dziala (100%)** | Bezpieczne wykrywanie folderow `bin/obj` (.NET), `node_modules` (Node.js) i `target` (Rust) na podstawie obecnosci plikow manifestow (`.csproj`, `package.json`, `Cargo.toml`). Domyslnie niezaznaczone. |
| **Katalog Programow** | `SoftwareInstallerService.cs` | **Dziala (100%)** | Odczyt stanu pakietow winget. |
| **System Ustawien i Motywow** | `SettingsService.cs`, `AppSettings.cs`, `App.xaml` | **Dziala (100%)** | Obsluga motywu ciemnego (`Dark`) i jasnego (`Light`). Zapis preferencji do pliku JSON w profilu uzytkownika. |
| **Nowy Silnik Skanera & Wynikow** | `IScanService.cs`, `IFixService.cs`, `ScanResultItem.cs` | **Nowy (Wdrazany w 6.0)** | Ujednolicony silnik integrujacy wszystkie moduly w jeden proces skanowania ze wskaznikiem zdrowia PC i lista wynikow. |
| **Trwala Historia Operacji** | `IHistoryService.cs` | **Nowy (Wdrazany w 6.0)** | Rejestr przeszlych operacji czyszczenia i optymalizacji z zapisem w `%LocalAppData%\Aetherial\history.json`. |
| **Punkt Przywracania** | `ISystemRestoreService.cs` | **Nowy (Wdrazany w 6.0)** | Ochrona systemu przed operacjami zbiorczymi. |

---

## 2. Wyeliminowane Antywzorce i Martwy Kod

1. **Usunieto statyczne kafelki**: Wyeliminowano sztywne karty w XAML symulujace usterki sprzetowe.
2. **Usunieto sztuczne wykresy**: Aplikacja nie generuje losowych wykresow liniowych ani fikcyjnych wskaznikow procentowych bez zrodla danych systemowych.
3. **Zabezpieczono testy przed utrata danych**: Testy regresji (`Aetherial.Regression`) wykonuja operacje wylacznie na izolowanych folderach w `%TEMP%` z unikalnym GUID-em.
4. **Czyste repozytorium**: Z repozytorium usunieto stare pliki wykonywalne, zrzuty pamieci, logi sesji i raporty HTML specyficzne dla pojedynczego komputera.

---

## 3. Zakres MVP Wersji 6.0

Wersja 6.0 skupia sie na 5 kluczowych filarach w oparciu o ergonomiczny wzorzec **IObit Driver Booster**:
1. **Pulpit Glowny**: Centralny wskaznik zdrowia PC (Circular Gauge) + 1 wielki przycisk akcji `[ ▶ SKANUJ TERAZ ]` + 4 kafelki statusowe.
2. **Skaner & Wyniki**: Przejrzysty kreator problemow z selekcja i masowa naprawa `[ Napraw zaznaczone ]`.
3. **Centrum Narzedzi**: Zintegrowany dostep do bezpiecznego czyszczenia, woluminow, dowiazan symbolicznych, projektow dev, inwentaryzacji sprzetu i programow.
4. **Historia**: Przeglad wykonanych optymalizacji i odzyskanego miejsca.
5. **Ustawienia**: Konfiguracja zachowania, domyslnego folderu i przelacznik motywu Jasny/Ciemny.
