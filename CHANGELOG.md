# CHANGELOG: Aetherial Suite

Wszystkie istotne zmiany w projekcie są dokumentowane w tym pliku.
Format jest oparty na [Keep a Changelog](https://keepachangelog.com/pl/1.0.0/), a wersjonowanie na [Semantic Versioning](https://semver.org/).

## [6.2.0] - 2026-09-14

### Dodano
- Stały pasek tematów w modułach Narzędzi i trzy działy Ustawień.
- Widok preferencji z sześcioma działającymi ustawieniami: motyw, cykliczne odczyty, interwał, automatyczny skan sprzętu/cache po wejściu oraz próg dużych plików.
- Walidację i atomowy zapis preferencji z jawnym raportowaniem błędów.
- 13 sprawdzeń ustawień w ScanTests oraz scenariusze WPF dla nawigacji, timera, progu i przewijania.
- Instrukcję używania dostępnego skryptu instalacji lokalnej z kontrolą sum, skrótami i deinstalacją zachowującą dane użytkownika.

### Zmieniono
- Zwarty pulpit i hierarchię danych oraz działań.
- Wspólne style pasków przewijania, pól wyboru, list rozwijanych i postępu.
- Stosowanie zapisanych ustawień w bieżącej sesji; próg dużych plików obowiązuje przy następnym wyszukiwaniu.

### Ograniczenia
- Automatyzacja odczytu nie wykonuje czyszczenia ani instalacji sterowników.
- Zakres katalogu NVIDIA, częściowa migracja MVVM i brak samoczynnego aktualizatora pozostają bez zmian.
- Wyniki lokalnych testów i status publikacji są oddzielnie opisane w docs/WERYFIKACJA.md.

## [6.1.0] - 2026-09-13

### Dodano
- Niezależne porównywanie sterowników NVIDIA z oficjalnym katalogiem na podstawie PCI ID, modelu i systemu; widok wersji, źródła, daty oraz filtrów wyników.
- Nowy przepływ Czyść: skan, wybór kategorii, potwierdzenie, postęp, anulowanie i rzeczywisty raport.
- Projekty Core i Services, kontener DI, view modele CommunityToolkit.Mvvm oraz rotowane logi Serilog w profilu użytkownika.
- Testy offline sterowników i skanera w procesie wydania.
- Test interakcji WPF przed publikacją oraz blokadę drugiej instancji aplikacji.

### Poprawiono
- Nawigację woluminów: normalizacja ścieżek, anulowanie poprzedniego odczytu i ochrona przed spóźnionymi wynikami.
- Zbiorcze wykonywanie: sprawdzanie celów względem katalogu, wymagany punkt przywracania i rozróżnienie wykonania, pominięcia, błędu oraz anulowania.
- Raport skanu: zakres dostępnych celów czyszczenia zamiast fikcyjnej kondycji PC; brak deklaracji pełnej naprawy po wykonaniu instrukcji.
- Trwałą historię: atomowy zapis, ograniczenie liczby wpisów, synchronizacja i obsługa uszkodzonego pliku.
- Retencję wyników kompilacji nowych bibliotek i projektów testowych.

### Ograniczenia
- AMD, Intel i OEM pozostają niezweryfikowane automatycznie. Nie dodano automatycznej instalacji sterowników producenta.
- Migracja wszystkich starszych narzędzi do MVVM, instalator i aktualizator pozostają w backlogu.
- Opis 6.0 poniżej dokumentuje wcześniejszy zakres; deklaracje procentowego zdrowia i pełnego skanu zostały zastąpione w 6.1 rzeczywistym zakresem pomiaru.

## [6.0.0] - 2026-09-13

### Dodano
- **Architektura IObit Driver Booster**:
  - Nowoczesny panel nawigacji bocznej (~200px) z 5 sekcjami: Główna, Skan, Narzędzia, Historia, Ustawienia.
  - Centralny wskaźnik stanu zdrowia PC (Circular Gauge) na Pulpicie ze zintegrowanym procentem i stanem bezpieczeństwa.
  - Jeden wielki przycisk akcji `[ ▶ SKANUJ TERAZ ]` inicjujący pełne skanowanie systemu.
  - Kreator skanowania i wyników: animowany wskaźnik, grupowanie wg ważności (Krytyczne, Ostrzeżenia, Do optymalizacji) oraz masowa naprawa `[ Napraw zaznaczone ]`.
- **Nowe serwisy domenowe (Core & Services)**:
  - `IScanService` / `ScanService`: ujednolicony silnik diagnostyczny agregujący stan dysków, RAM, PnP, woluminów i artefaktów deweloperskich.
  - `IFixService` / `FixService`: bezpieczny wykonawca napraw z raportowaniem postępu i ochroną integralności systemu.
  - `IHistoryService` / `HistoryService`: trwała historia operacji zapisywana w `%LocalAppData%\Aetherial\history.json`.
  - `ISystemRestoreService` / `SystemRestoreService`: opcjonalny punkt przywracania systemu Windows przed działaniami naprawczymi.
  - Ujednolicony model `ScanResultItem` ze statusem, rozmiarem, kategorią i wagą problemu.
- **Trwała Historia Operacji**:
  - Dedykowana zakładka `📜 Historia` z tabelą minionych optymalizacji, odzyskanego miejsca i znacznikami czasu.
- **Dokumentacja**:
  - `AUDIT.md`: kompletna inwentaryzacja funkcji i stanu modułów.

### Zmieniono
- Przeprojektowano widok główny z wąskiego 84px rail na czytelny, ergonomiczny panel ~200px.
- Zunifikowano Centrum Narzędzi (`Narzędzia`), grupując zaawansowane moduły (Czyszczenie, Symlinki, Projekty Dev, Sprzęt PnP, Programy).
- Wzbogacono testy regresji i smoke o weryfikację nowych serwisów skanera i historii.

### Usunięto
- Wszystkie pozostałości po dawnych statycznych kafelkach ze sztucznymi kodami błędów.
- Zbędne, zduplikowane pliki binarne z katalogu źródłowego.
