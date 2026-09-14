# Aetherial 6.1.0

- Sprzęt ma własny przepływ sprawdzania: zainstalowana wersja, wersja producenta,
  źródło, czas weryfikacji i filtry. Katalog NVIDIA jest sprawdzany według PCI ID,
  modelu karty i Windows x64. Pozostali producenci pozostają niezweryfikowani.
- Nowy widok Czyść prowadzi od skanu przez wybór kategorii do wyniku. Domyślnie
  niczego nie zaznacza; pokazuje postęp i umożliwia anulowanie.
- Zbiorcze czyszczenie sprawdza cele względem aktualnego katalogu, wymaga punktu
  przywracania i zapisuje rzeczywisty wynik do historii. Punkt przywracania
  nie jest kopią usuwanych plików. Anulowanie nie cofa wykonanych usunięć.
- Zmiana woluminu w Dyskach normalizuje ścieżkę, anuluje starszy odczyt i chroni
  listę przed zastąpieniem wynikami poprzedniej nawigacji.
- Skan nie deklaruje fikcyjnej kondycji PC ani skutecznej naprawy sterowników.
- Oddzielono projekty Core, Services i WPF, dodano DI, view modele i rotowane logi.
- Proces wydania uruchamia izolowane testy regresji, sterowników i skanera;
  utrzymuje jeden lokalny pakiet i jeden najnowszy opublikowany GitHub Release.

Katalog NVIDIA dotyczy Game Ready WHQL DCH; Studio i pakiety OEM mogą się różnić.
Nie ma automatycznej instalacji sterowników producenta, instalatora aplikacji ani
samoczynnych aktualizacji Aetherial. Starsze narzędzia nie przeszły jeszcze pełnej
migracji MVVM. Szczegóły testów i ograniczeń: docs/WERYFIKACJA.md w repozytorium.
