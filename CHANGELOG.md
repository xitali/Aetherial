# CHANGELOG: Aetherial Suite

Wszystkie istotne zmiany w projekcie są dokumentowane w tym pliku.
Format jest oparty na [Keep a Changelog](https://keepachangelog.com/pl/1.0.0/), a wersjonowanie na [Semantic Versioning](https://semver.org/).

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
