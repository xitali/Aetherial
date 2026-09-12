# Weryfikacja Aetherial 6.0.0

## Zrealizowane

- Zaktualizowany brief i plan obejmujący dziesięć obszarów odpowiedzialności.
- Pełna przebudowa dashboardu i widoku sprzętu; wspólne style pozostałych widoków.
- Rzeczywiste odczyty woluminów, RAM, uptime, CPU, GPU, sieci oraz 220 obecnych
  urządzeń PnP na komputerze testowym. Dane maszyny nie są częścią paczki ani Git.
- Poprawki błędów odczytu PowerShell: UTF-8, osobne stdout/stderr, błędy poleceń.
- Brak fikcyjnych procentów zdrowia, modelu sprzętu, odzyskanych GB i statusu aktualności.
- Ochrona plików: walidacja katalogów i dowiązań, filtry wieku, kontrola kodów procesów,
  brak trwałego usuwania jako awaryjnego zamiennika nieudanego przeniesienia do kosza.
- Migracja zachowuje kopię źródła; zwolnienie jej miejsca wymaga ręcznej weryfikacji.
- Ustawienia i logi poza projektem; raport na żądanie, kodowany HTML.

## Testy

- 22 sprawdzenia regresji na izolowanych plikach: chronione katalogi, filtry wieku,
  pliki zablokowane, anulowanie, własne cele, identyfikacja projektów, odznaczone
  artefakty, odrzucone migracje, aktualny pomiar rozmiaru, modele i proces PowerShell.
- 18 renderów prawdziwego drzewa WPF: siedem widoków w obu motywach przy 1366×900,
  dodatkowo dashboard przy 2560×1440 i 3840×2160 (200% DPI).
- Ręcznie obejrzane rendery dashboardu, sprzętu i narzędzi; poprawione tło,
  kontrast przycisków i neutralne statusy w jasnym motywie.
- Siedem tras nawigacji wybiera dokładnie jeden widok. Śledzenie błędów bindingów WPF
  nie zgłasza błędów dla katalogu aplikacji, urządzeń, celów czyszczenia i presetów.
- Release: build/test/publish do stagingu, kontrola pliku EXE i SHA-256, zamiana latest.

## Granice weryfikacji

Test renderowania uruchamia rzeczywisty XAML i usługi odczytu, lecz nie wywołuje Loaded,
instalacji ani usuwania danych użytkownika. Narzędzie automatyzacji pulpitu dwukrotnie
zwróciło błąd inicjalizacji „failed to write kernel assets”. Nie potwierdzono więc
pełnego testu klikania w otwartym oknie, UAC, instalacji sterowników, instalatorów winget,
ani migracji czynnych folderów. Nie wykonywano zmian ustawień bezpieczeństwa Windows.

Windows Update zależy od usługi i sieci, a część funkcji od administratora.
Nie ma pomiaru temperatur ani wykresu historycznego obciążenia CPU/GPU: aplikacja
pokazuje dostępne dane, bez generowania brakujących pomiarów. Domyślne lokalizacje
cache odpowiadają konwencjom programów; niestandardowe foldery mogą wymagać własnego celu.
Katalog aplikacji i linki producentów są danymi referencyjnymi, nie odczytem stanu PC.

Główny code-behind nadal koordynuje widoki. Dalszy podział na osobne view modele
jest możliwym następnym etapem, a nie deklarowaną częścią ukończonej migracji MVVM.
