# Audyt Aetherial 6.1

Data: 2026-09-13. Dokument opisuje stan implementacji i znane granice;
nie jest deklaracją, że wszystkie funkcje sprawdzono na każdym komputerze.

| Moduł | Stan implementacji | Granice |
| --- | --- | --- |
| Dyski i eksplorator | DriveInfo, normalizacja korzeni woluminów, anulowanie odczytu i odrzucanie spóźnionych wyników; jawny stan folderu. | Dyski odłączane, uprawnienia i pełna obsługa fizycznego pulpitu wymagają dalszych scenariuszy. |
| Sprzęt | Inwentaryzacja PnP/CIM i niezależny katalog NVIDIA; porównanie wersji z informacją o zgodności, źródle i dacie. | Game Ready WHQL DCH Windows x64; AMD/Intel/OEM nie są automatycznie porównywane. Brak instalatora sterowników producenta. |
| Czyść | Osobny widok i view model, skan, wybór, potwierdzenie, anulowanie, raport i historia. Cele są sprawdzane ponownie przed działaniem. | Konwencjonalne lokalizacje cache nie obejmują wszystkich konfiguracji. Zablokowane pliki mogą pozostać. |
| Skan i wykonawca | Raport dostępnych celów czyszczenia, tylko obsługiwane działania; brak arbitralnego wskaźnika zdrowia oraz automatycznej naprawy PnP. | Nie jest to pełna diagnostyka sprzętu. Starsze przepływy wymagają dalszego ujednolicenia. |
| Punkt przywracania | Sprawdzenie utworzenia przed zbiorczym czyszczeniem; niepowodzenie zatrzymuje wykonanie. | Wymaga obsługi przez system i uprawnień. Nie odtwarza usuwanych plików użytkownika. |
| Historia | Ograniczony do 100 wpisów, synchronizowany zapis atomowy JSON; zachowanie uszkodzonego pliku i zgłoszenie błędu. | Awaria zapisu jest zgłaszana, nie gwarantuje utrwalenia operacji. |
| Architektura | Core bez WPF, Services, aplikacja UI, DI i Serilog; nowe view modele oparte na CommunityToolkit.Mvvm. | Część starszych usług oraz MainWindow nadal koordynuje logikę; migracja MVVM nie jest zakończona. |
| Narzędzia zaawansowane | Zachowano migracje folderów, projekty dev, RAM, winget i Windows Update. | Istnienie funkcji nie potwierdza poprawności każdego scenariusza produkcyjnego; nie uruchamiano masowych zmian na danych użytkownika. |
| Wydania | Staging przed zamianą latest, SHA-256, testy offline, ignorowanie binariów/logów i retencja jednego release po udanej publikacji. | Stan konkretnego wydania i testów jest dokumentowany osobno. Instalator i aktualizator pozostają w backlogu. |

## Dane referencyjne i pomiary

Modele sprzętu, wersje lokalne, dostępne woluminy i rozmiary są odczytywane.
Dopuszczalne stałe to reguły katalogu cache, identyfikatory API systemów, identyfikatory
winget i oficjalne adresy wsparcia. Nie stanowią one wyniku diagnozy komputera.
Błąd katalogu sterowników nie daje statusu „aktualny”; wynik PnP nie potwierdza
najnowszej wersji. Nie należy prezentować procentu zdrowia bez uzasadnionego pomiaru.

## Weryfikacja i kolejne etapy

[WERYFIKACJA.md](docs/WERYFIKACJA.md) oddziela wykonane testy od niewykonanych.
[BACKLOG.md](BACKLOG.md) zawiera kryteria pozostałych prac wynikających z
[AETHERIAL_PLAN.md](AETHERIAL_PLAN.md). Plan użytkownika zachowano bez zmiany.
