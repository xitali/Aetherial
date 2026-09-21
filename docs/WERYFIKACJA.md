# Weryfikacja Aetherial 6.3.0

Sprawdzenia lokalne: 2026-09-21. Wyniki dotyczą przebudowanego interfejsu i pakietu 6.3.

## Wykonane

- Release build: 0 błędów, 0 ostrzeżeń.
- Regression: 41 sprawdzeń, w tym parser rzeczywistej tabeli WinGet, źródła,
  dokładne ID, reset starego stanu, fallback, wersje i blokada niezweryfikowanych instalacji.
- DriverTests: 25 sprawdzeń dopasowania modeli/systemu, wersji i błędów źródeł.
- ScanTests: 30 sprawdzeń skanowania, wykonania, historii oraz trwałych ustawień.
- WPF: wszystkie 7 sekcji, powrót z 4 kategorii narzędzi, wybór rzeczywistych
  woluminów, anulowanie/spóźnione odpowiedzi, stany czyszczenia, szczegóły,
  Programy (instalacje/aktualizacje/katalog), wyszukiwanie, timer, próg i scrollbar.
- 84 rendery obu motywów przy różnych rozmiarach i DPI; kontrola wizualna głównych
  ekranów i szczegółów. Dane wyników czyszczenia/historii w renderach to jawne
  fixtures testowe. Odczyty woluminów, sprzętu i programów pochodzą z systemu.
- Bieżący odczyt WinGet: 158 programów, 11 aktualizacji, pełna odpowiedź parsera.
  Katalog sprzętu: 219 urządzeń, 1 zweryfikowana karta NVIDIA; inne bez potwierdzenia.
- release.ps1 zakończył pełny przebieg, zachował tylko release/latest i usunął
  bin/obj znanych projektów. Zastąpiono instalację 6.2 wersją 6.3, sprawdzono SHA-256
  i wersję wpisu deinstalacji. Uruchomiony proces odpowiada; przechwycono jego okno.

## Granice dowodów

Testy WPF wywołują zdarzenia w rzeczywistym drzewie kontrolek. Harness używa tych
samych zasobów Themes/Controls.xaml, ale nie uruchamia produkcyjnego StartupUri.
Nie są to fizyczne kliknięcia w pulpicie. Narzędzie computer-use odczytało zrzut
zainstalowanego, podniesionego procesu, lecz próby kliknięcia nie zmieniły widoku.
Pełny test pulpitu z UAC/klawiaturą pozostaje niepotwierdzony.

Nie uruchamiano usuwania danych użytkownika ani instalacji sterowników/programów
w ramach QA. Odczyt jednej karty NVIDIA nie dowodzi pokrycia AMD/Intel/OEM.
Pełne kryteria planu pozostają otwarte w BACKLOG.md. Podpisany instalator,
automatyczny aktualizator, pełna migracja MVVM i zasoby językowe nie są ukończone.

Publikację GitHub i retencję jednej paczki należy sprawdzać w wynikach workflow
bieżącego taga; powyższy raport nie zastępuje wyniku CI.
