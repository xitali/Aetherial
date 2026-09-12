# Plan wdrożenia Aetherial 6.0

## Cel i decyzje

Przebudowa istniejącej aplikacji WPF: czytelny polski interfejs, prawdziwe dane,
bezpieczne operacje oraz jedno powtarzalne wydanie. Nie migrujemy do stacku webowego.
Zachowujemy narzędzia; usuwamy fikcyjne wyniki, osobiste raporty i stare artefakty.

## Dziesięć obszarów odpowiedzialności

1. Architektura: zachowanie kontraktów usług, oddzielenie prezentacji od odczytów.
2. UX: podsumowanie → diagnoza → świadomie uruchamiana operacja → wynik.
3. Design system: wspólne zasoby WPF, dark/light, focus, statusy, typografia.
4. Dashboard: rzeczywiste woluminy, RAM, czas pracy i uczciwe stany braku danych.
5. Sprzęt: PnP/CIM, bez profilu konkretnego komputera i sugerowanych fikcyjnych usterek.
6. Operacje plikowe: walidacja ścieżek, kontrola kopiowania, ochrona źródła.
7. Automatyzacja: odczyty i odświeżanie bez automatycznych destrukcyjnych działań.
8. Ustawienia: dane użytkownika poza kodem i paczką, wartości zależne od środowiska.
9. QA: kompilacja, testy regresji, uruchomienie i kontrola obu motywów.
10. Release: staging przed wymianą, jeden katalog latest, kod bez binariów na GitHub.

Zakres realizują równolegle cztery dostępne procesy agentów; powyższe punkty to role,
a nie deklaracja uruchomienia dziesięciu niezależnych agentów.

## Kolejność

1. Audyt kodu, danych, stanu Git i istniejących artefaktów.
2. Równoległa implementacja UI, zachowania i usług; skoordynowane nazwy kontrolek.
3. Integracja, testy na sztucznych plikach i odczyty rzeczywistego systemu.
4. Aktualizacja dokumentacji, kompilacja wydania i kontrola zawartości paczki.
5. Usunięcie starych buildów po pomyślnym przygotowaniu nowego.
6. Publikacja kodu i najnowszego release, jeśli dostępne uwierzytelnienie GitHub.

## Kryteria akceptacji

- Żadnego deklarowanego procentu zdrowia, temperatury ani wersji sterownika bez odczytu.
- Brak danych/błąd/skanowanie odróżnione od poprawnego stanu.
- Zachowane wejścia do czyszczenia, eksploratora, aplikacji, diagnostyki i narzędzi.
- Brak czyszczenia systemu użytkownika podczas testów.
- Nieudane wydanie nie usuwa poprzedniego działającego pakietu.
- Repozytorium bez buildów, logów i raportów o konkretnym komputerze.
- Wyniki testów oraz ograniczenia opisane w raporcie końcowym.
