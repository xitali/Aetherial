# Weryfikacja Aetherial 6.2.0

Stan lokalnych sprawdzeń z 2026-09-14. Końcowy pakiet i publikacja 6.2 wymagają
oddzielnego potwierdzenia; poniższe wyniki nie deklarują powodzenia GitHub CI.

## Testy lokalne

- `Aetherial.Regression`: 26 sprawdzeń zakończonych powodzeniem; m.in. ochrona
  ścieżek, filtry wieku, anulowanie, pliki zablokowane i rzetelność pomiarów.
- `Aetherial.DriverTests`: 25 sprawdzeń offline zakończonych powodzeniem; dopasowanie
  PCI/modelu/systemu, wersje, adresy źródłowe, brak pokrycia, błędy sieci i anulowanie.
- `Aetherial.ScanTests`: 30 sprawdzeń zakończonych powodzeniem. Do skanera,
  wykonawcy i historii dodano 13 sprawdzeń ustawień: zapis wszystkich preferencji,
  odczyt po ponownym utworzeniu usługi, walidację zakresów, zachowanie poprawnej
  konfiguracji przy błędzie, uszkodzony JSON i sprzątanie plików transakcji.
- Testy wewnątrz procesu WPF potwierdziły nawigację po tematach, działanie timera
  odświeżania, wpływ progu na wyszukiwanie dużych plików oraz działanie paska
  przewijania. Są to zdarzenia w rzeczywistym drzewie WPF, nie fizyczne kliknięcia
  w otwartym oknie na pulpicie.

Końcowe rendery interfejsu po zmianach 6.2 będą odświeżone osobno. Wyników i liczby
renderów poprzedniej wersji nie należy przypisywać bieżącemu wyglądowi.
Proces wydania uruchamia testy offline i `Aetherial.VisualSmoke --interactions-only`
przed przygotowaniem pakietu. Końcowy lokalny przebieg wydania zakończył się powodzeniem.

## Zakres niewykonany i ograniczenia

- Nie potwierdzono pełnego testu na fizycznym pulpicie: instalacji sterowników,
  winget, dwóch równoległych uruchomień ani migracji czynnych folderów.
- `scripts/install-local.ps1` jest dostępny, lecz obecność skryptu nie stanowi dowodu
  instalacji końcowego pakietu 6.2. Nie dodano automatycznego aktualizatora.
- Nie wykonywano zbiorczych operacji na danych użytkownika. Testy korzystają
  z izolowanych plików lub zastępczych zależności. Punkt przywracania nie jest
  kopią kasowanych plików, a anulowanie nie cofa wykonanych usunięć.
- Automatyczne porównywanie sterowników obejmuje katalog NVIDIA Game Ready WHQL
  DCH. Pozostali producenci są niezweryfikowani; brak dostępu do katalogu nie daje
  statusu „aktualny”. Nie ma pełnej diagnostyki zużycia SSD ani temperatur.
- Niedostępne podkatalogi i niestandardowe lokalizacje mogą ograniczać pomiar cache.
  Starsze narzędzia pozostają częściowo w code-behind.

Lista pozostałych prac: [BACKLOG.md](../BACKLOG.md).
