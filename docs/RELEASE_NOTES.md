# Aetherial 6.0.0

- Przebudowany polski interfejs WPF: spokojny dashboard, wspólne style, dark/light.
- Rzeczywiste dyski i urządzenia zamiast profilu przykładowego komputera.
- Rozróżnienie odczytu, braku danych i problemu; usunięcie fikcyjnych wyników zdrowia.
- Poprawki walidacji ścieżek, obsługi procesów i ochrony danych przy migracji folderów.
- Ustawienia i logi użytkownika poza folderem aplikacji.
- Raport tworzony z bieżących danych; brak gotowego raportu konkretnego komputera.
- Powtarzalny build/test/publish i jeden katalog release/latest.
- GitHub Actions z publikacją pakietu oraz retencją jednego opublikowanego release.

Operacje administracyjne wymagają świadomego wyboru. Wynik odczytu PnP nie jest
gwarancją najnowszego sterownika; brak usterki PnP nie oznacza pełnej diagnostyki sprzętu.
Szczegóły przeprowadzonych testów i ograniczeń: docs/WERYFIKACJA.md w repozytorium.
