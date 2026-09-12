# Aetherial Suite — brief wdrożeniowy 6.0

Przebudowa istniejącej aplikacji Windows Storage & Diagnostics w czytelne narzędzie
po polsku. Zachować funkcje, poprawić błędy i usunąć fikcyjne dane.

## Technologia i wygląd

- Zachować C# / .NET 8 / WPF, istniejące usługi i integracje Windows.
- Wspólne zasoby: kolory, typografia, odstępy, promienie, statusy i focus.
- Domyślny dark theme oraz light theme z zapisem preferencji.
- Hierarchia: podsumowanie, szczegóły, świadome działanie, wynik.
- Bez dekoracyjnych wykresów z wymyślonym przebiegiem ani fikcyjnych kart sprzętu.
- Przewijanie, skalowanie DPI i zawijanie długich nazw.

## Dane i funkcje

- Dyski z wykrytych woluminów, nigdy z listy C/D/E/F.
- RAM, uptime, CPU, GPU, sieć i PnP z Windows; nieznane wartości opisane.
- Dostępność urządzenia nie oznacza aktualności sterownika ani zdrowia SSD.
- Katalog aplikacji może być stały; stan instalacji i aktualizacji musi być odczytany.
- Zachować narzędzia, grupując je według zadań. Automatyzować odczyty, a operacje
  ryzykowne wykonywać po świadomym wyborze i przedstawiać ich wynik.
- Kopiowanie i przenoszenie sprawdza kod zakończenia i chroni oryginał.
- Raporty zawierają odczytane dane i czas pomiaru, z kodowaniem HTML.
- Ustawienia, logi i raporty użytkownika poza repozytorium i paczką.

## Release i weryfikacja

- Jedna wersja produktu w pliku projektu, jeden pakiet w release/latest.
- Skrypt scripts/release.ps1 najpierw kompiluje i testuje, potem wymienia paczkę.
- GitHub: kod, zasoby, dokumentacja i testy; binaria wyłącznie w release.
- Workflow usuwa poprzednie opublikowane release dopiero po przesłaniu nowego.
- Usunąć stare buildy, tymczasowe raporty i nieaktualne instrukcje.
- Build, regresje na izolowanych plikach, uruchomienie WPF, nawigacja i oba motywy.
- Jawnie opisać ograniczenia; nie testować usuwania na danych użytkownika.

Szczegółowy plan: docs/PLAN_WDROZENIA.md.
Aetherial - Plan Restrukturyzacji Aplikacji
Filozofia projektu
UI to tylko skóra. Najpierw serce (logika), potem wygląd.
80% chaosu znika, gdy logika i interfejs są fizycznie rozdzielone.

Wzorzec wizualny: IObit Driver Booster
┌──────────────────────────────────────────────────────────────┐
│ ◆ AETHERIAL  v1.0          ⚙  ?  ─  □  ✕   ← pasek tytułu   │
├───────────┬──────────────────────────────────────────────────┤
│           │                                                  │
│  🏠 Główna │        ┌────────────────────────┐                │
│  🔍 Skan  │        │      87%  ◉ (gauge)    │                │
│  🛠 Narzędzia│      │   "Znaleziono 12       │                │
│  📜 Historia│       │    problemów"          │                │
│  ⚙ Ustawienia│     │                        │                │
│           │        │  [  ▶ SKANUJ TERAZ  ]  │  ← wielki CTA  │
│  ~200px   │        └────────────────────────┘                │
│  sidebar  │                                                  │
│           │   ┌─karta─┐ ┌─karta─┐ ┌─karta─┐                  │
│           │   │Moduł 1│ │Moduł 2│ │Moduł 3│                  │
├───────────┴───┴───────┴─┴───────┴─┴───────┴──────────────────┤
│ ● Gotowy  │  Wersja 1.0.0  │  Ostatni skan: dziś 14:02       │
└──────────────────────────────────────────────────────────────┘


ETAP 0 – Porządki i audyt (2-3 dni)
Cel: przestać tonąć. Nie piszesz nic nowego, tylko sprzątasz.

Zadania:
 Stwórz plik AUDIT.md - inwentaryzacja funkcji
 Lista: co działa / co pół-działa / co jest martwe
 Dodaj/popraw .gitignore (usuń bin/, obj/, .vs/, buildy)
 Uporządkuj gałęzie Git (jedna główna main, praca na feature/...)
 Dodaj LICENSE, README.md, CHANGELOG.md
 Wywal martwy kod (zakomentowane bloki, nieużywane pliki)
 Zdefiniuj zakres MVP (3-4 funkcje do perfekcji, reszta do BACKLOG.md)
Kryterium ukończenia: Repo czyste, wiesz co masz, wiesz co budujesz.

ETAP 1 – Architektura: fundament (1-2 tygodnie)
Cel: logika odseparowana od UI.

Struktura rozwiązania:
Aetherial.sln
├── Aetherial.Core          → logika domenowa, interfejsy, modele. ZERO referencji do UI.
├── Aetherial.Services      → implementacje: skanowanie, czyszczenie, operacje systemowe
├── Aetherial.UI            → tylko widoki, viewmodele, zasoby graficzne
└── Aetherial.Tests         → testy jednostkowe Core i Services

Zadania:
 Podziel projekt na warstwy (Core / Services / UI / Tests)
 Wprowadź DI: Microsoft.Extensions.DependencyInjection
 Konfiguracja w jednym miejscu: appsettings.json + SettingsService
 Logowanie: Serilog → %LocalAppData%\Aetherial\logs
 Globalna obsługa błędów (jeden punkt, okno dialogowe + log)
 Dodaj .editorconfig + analyzery Roslyn
Kryterium ukończenia: UI nie zawiera ani jednej linijki logiki biznesowej.

ETAP 2 – Refaktoryzacja logiki (1-2 tygodnie)
Cel: każda operacja to serwis z przewidywalnym zachowaniem.

Serwisy do wyodrębnienia:
 IScanService → wykrywanie problemów
 IFixService → wykonywanie napraw
 ISystemRestoreService → punkt przywracania przed zmianami
 IHistoryService → dziennik operacji
Zadania:
 Wszystko długotrwałe = async z IProgress<T> i CancellationToken
 Zawsze operacja w tle, nigdy blokowanie wątku UI
 Ujednolicony model wyników (ScanResultItem)
 Automatyczny punkt przywracania przed destrukcyjną akcją
 Potwierdzenie + podgląd co dokładnie zostanie zmienione
 Rejestr operacji w historii
Model wyników:
public record ScanResultItem(
    string Name, string Description, Severity Severity,
    long SizeBytes, FixAction Fix, string DetailPath);
	
	Kryterium ukończenia: Każdą funkcję da się wywołać z konsoli/testu bez UI.

ETAP 3 – UI w stylu IObit (2-3 tygodnie)
Cel: wygląd profesjonalnej aplikacji desktopowej.

Layout (sztywny, niezmienny między widokami):
 Lewy panel nawigacji (~200px): duże ikony + etykiety
 Zaznaczony element z podświetleniem
 Środek = jeden widok na raz (Page/UserControl per zakładka)
 Dolny pasek statusu zawsze widoczny
Widok główny (Dashboard):
 Centralny licznik/gauge ("stan zdrowia" lub wynik skanu w %)
 JEDEN wielki przycisk akcji (nigdy 5 obok siebie)
 Pod nim karty podsumowujące moduły
Widok wyników:
 Lista: [checkbox] Nazwa | opis | rozmiar | [napraw]
 Na dole zielony przycisk „Napraw zaznaczone (N)"
Paleta kolorów:
 Ciemny granat tła, jaśniejsze karty
 Cyjan/granatowy - kolor główny
 Zieleń - akcje pozytywne
 Pomarańcz/czerwień - tylko ostrzeżenia
 Zaokrąglone rogi (8-12 px), delikatne gradienty
 Jeden zestaw ikon (Fluent Icons / Material Design Icons)
Implementacja (WPF/.NET):
 MVVM przez CommunityToolkit.Mvvm
 Nawigacja: ContentControl + DataTemplate per widok
 Stany UI jako enum: Idle / Scanning / Results / Fixing / Done / Error
 Animacje: płynne przejścia, obracający się wskaźnik podczas skanu
 DPI awareness w manifeście
Kryterium ukończenia: Użytkownik w 5 sekund rozumie, co robi aplikacja i gdzie kliknąć.

ETAP 4 – Przepływy krok po kroku (1 tydzień)
Cel: aplikacja prowadzi użytkownika za rękę.

Każda operacja = kreator o stałych krokach:
Skanuj → pasek postępu, co sprawdza, ile znaleziono
Wyniki → podsumowanie, możliwość odznaczenia elementów
Potwierdź → co dokładnie zostanie zrobione + opcja punktu przywracania
Wykonaj → postęp per element, można anulować
Raport → co naprawiono, co pominęto, zapis w historii
Zadania:
 Zaimplementuj kreator (wizard) dla każdej operacji
 Zakładka Historia (lista przeszłych operacji z datą i wynikiem)
ETAP 5 – Jakość (równolegle, domknięcie: 1 tydzień)
 Testy jednostkowe (Core + Services): pozytywne ścieżki, brak uprawnień, anulowanie
 Manifest requireAdministrator (jeśli operujesz na systemie)
 Grzeczna obsługa odmowy uprawnień
 Edge case'y: foldery tylko-do-odczytu, długie ścieżki, podwójne uruchomienie (mutex), brak internetu
 Lokalizacja: wszystkie stringi do .resx od razu (PL/EN)
ETAP 6 – Dystrybucja (2-3 dni)
 GitHub Actions: build + testy przy każdym push
 Release z instalatorem przy tagu v*
 Instalator: Inno Setup lub MSIX (ikona, skróty, deinstalacja)
 Wersjonowanie semantyczne + automatyczna aktualizacja (Velopack)
 README z animowanym screenshotem/GIF-em
Harmonogram
| Etap | Co | Czas | Zależny od |
|------|-----|------|-----------|
| 0 | Porządki | 2-3 dni | – |
| 1 | Architektura | 1-2 tyg. | 0 |
| 2 | Logika | 1-2 tyg. | 1 |
| 3 | UI IObit-style | 2-3 tyg. | 2 |
| 4 | Przepływy | 1 tydz. | 3 |
| 5 | Jakość | 1 tydz. | równolegle |
| 6 | Dystrybucja | 2-3 dni | 4-5 |


