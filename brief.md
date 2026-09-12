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
