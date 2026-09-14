# Aetherial 6.2.0

- Stały pasek tematów w modułach Narzędzi pozwala przechodzić pomiędzy nimi bez
  wracania do strony wyboru. Pulpit jest bardziej zwarty, z czytelniejszą hierarchią
  bieżących danych i akcji.
- Ustawienia podzielono na „Wygląd i odczyty”, „Programy i pliki” oraz „Windows
  i raporty”. Preferencje sterują działaniem aplikacji: motywem, cyklicznym odczytem
  parametrów, interwałem odświeżania, skanem sprzętu i cache po otwarciu modułu
  oraz progiem rozmiaru podczas wyszukiwania dużych plików.
- Zapisane preferencje są walidowane i stosowane w bieżącej sesji. Błędy odczytu
  oraz zapisu są zgłaszane; nieprawidłowa konfiguracja nie zastępuje poprawnego pliku.
- Ujednolicono paski przewijania, pola wyboru, listy rozwijane i wskaźniki postępu.
- Rozszerzono izolowane testy ustawień oraz testy zdarzeń WPF o nawigację,
  rzeczywisty timer odświeżania, próg wyszukiwania i przewijanie.

Automatyczny skan odczytuje dane. Nie wybiera plików do usunięcia ani nie instaluje
sterowników. Weryfikacja katalogu producenta nadal obejmuje NVIDIA Game Ready WHQL
DCH na Windows 10/11 x64; pozostałe urządzenia pozostają niezweryfikowane.

W repozytorium dostępny jest `scripts/install-local.ps1`: instaluje przygotowany
pakiet `release/latest` w profilu użytkownika, sprawdza sumę EXE, tworzy skróty
oraz wpis deinstalacji. To lokalny skrypt instalacyjny; automatyczny aktualizator
Aetherial nie został dodany. Instrukcja i ograniczenia testów znajdują się w README
oraz docs/WERYFIKACJA.md.
