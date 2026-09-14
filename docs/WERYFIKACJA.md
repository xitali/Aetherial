# Weryfikacja Aetherial 6.1.0

Stan lokalnych sprawdzeń z 2026-09-13, przed publikacją wydania.

## Potwierdzone

- Kompilacja Release: 0 błędów, 0 ostrzeżeń.
- `Aetherial.Regression`: 26 sprawdzeń zakończonych powodzeniem. Obejmują m.in.
  ochronę ścieżek, filtry wieku, anulowanie, zablokowane pliki oraz pomiar rozmiaru.
- `Aetherial.DriverTests`: 25 sprawdzeń offline zakończonych powodzeniem. Testują
  dopasowanie PCI/modelu/systemu, porównanie wersji, zaufane adresy, błędy sieci,
  brak pokrycia producenta i anulowanie. Dane testowe są jawnie izolowane od aplikacji.
- `Aetherial.ScanTests`: 17 sprawdzeń zakończonych powodzeniem. Obejmują skaner,
  walidację wykonania, odmowę bez punktu przywracania, anulowanie i historię.

Testy nie są zezwoleniem na usuwanie danych użytkownika. Operacje testowe korzystają
z izolowanych danych lub zastępczych implementacji zależności.

## Kontrola UI i publikacji

- Test zdarzeń w rzeczywistym drzewie WPF: kliknięcia czterech wykrytych woluminów
  oraz anulowanie skanu czyszczenia zakończone powodzeniem.
- 22 rendery bieżącego XAML, dziewięć tras nawigacji, zero wykrytych błędów bindingów.
  Po zmianie kontrastu trwa ponowne renderowanie; kontrola wcześniejszych renderów
  nie zastępuje oceny końcowego wyglądu.
- Proces wydania uruchamia także `Aetherial.VisualSmoke --interactions-only` przed
  publikacją. Jest to test obsługi zdarzeń wewnątrz procesu WPF.
- Dodano blokadę drugiej instancji aplikacji przez mutex. Potwierdzono obecność
  obsługi w kodzie; scenariusza dwóch uruchomień na pulpicie jeszcze nie wykonano.

Lokalny release 6.1.0 przygotowano przez scripts/release.ps1; pozostał tylko release/latest.
Stare buildy bin/obj usunięto. Rendery testowe pozostają w ignorowanym .artifacts/visual.
Status publikacji można sprawdzić w GitHub Actions i na stronie najnowszego wydania.

## Granice

Test drzewa WPF nie jest pełnym testem kliknięć na fizycznym pulpicie. W poprzednim
podejściu automatyzacja pulpitu nie zainicjalizowała się („failed to write kernel
assets”). Nie potwierdzono tą drogą UAC, instalacji sterowników, winget ani migracji
czynnych folderów. Nie wykonywano zbiorczych operacji na danych użytkownika.

Weryfikacja wersji producenta ma obecnie pokrycie NVIDIA Game Ready WHQL DCH;
pozostałe urządzenia są jawnie niezweryfikowane. Dostępność internetu i katalogu
może zmienić wynik. Punkt przywracania nie jest kopią kasowanych danych.
Brak pełnej diagnostyki zużycia SSD i temperatur. Niedostępne podkatalogi oraz
niestandardowe lokalizacje programów mogą ograniczyć pomiar cache.

Lista pozostałych prac: [BACKLOG.md](../BACKLOG.md).
