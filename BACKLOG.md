# Backlog Aetherial

Dokument uzupełnia plan użytkownika. Nie oznacza ukończenia wszystkich jego etapów.

| Priorytet | Praca | Kryterium ukończenia |
| --- | --- | --- |
| P1 | Dostawcy sterowników AMD, Intel i OEM | Oficjalne źródła, dopasowanie sprzętu i systemu, porównanie wersji, jawna obsługa braku danych; testy błędnych dopasowań. |
| P1 | Pełny test desktopowy na czystym Windows 10/11 | Rzeczywiste kliknięcia, UAC, DPI, klawiatura, brak ochrony systemu, sieć offline i odłączany dysk; oddzielony od testu drzewa WPF. |
| P1 | Pokrycie niestandardowych lokalizacji cache | Wykrywanie konfiguracji aplikacji, raport nieodczytanych podkatalogów i testy uprawnień; brak deklaracji pełnego skanu przy częściowym pomiarze. |
| P2 | Migracja starszych narzędzi do MVVM | Osobne widoki i kontrakty, usunięta logika operacji z MainWindow, testy zachowania symlinków/programów/projektów dev. |
| P2 | Ujednolicenie skanu i czyszczenia | Wspólny model wyniku, raport częściowego usunięcia i jedna implementacja walidacji wykonywania. |
| P2 | Instalator i aktualizator aplikacji | Wybrany format, podpis i weryfikacja pakietów, obsługa błędu/rollback, test aktualizacji poprzedniej wersji. |
| P2 | Zasoby językowe i dostępność | Teksty przeniesione do zasobów, test kontrastu/focusu/czytnika i pełna obsługa klawiatury. |
| P3 | Dodatkowa telemetria lokalna sprzętu | Wyłącznie dostępne odczyty z udokumentowanym źródłem; brak zastępczych temperatur i procentów zdrowia. |

Automatyczna instalacja sterowników wymaga osobnego projektu zgodności, podpisów,
obsługi restartów oraz testów odzyskiwania. Obecny link do producenta nie jest
równoznaczny z wdrożeniem takiego instalatora.
