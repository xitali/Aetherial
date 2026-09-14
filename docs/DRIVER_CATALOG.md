# Weryfikacja sterowników producenta

`DriverCatalogService` działa niezależnie od listy aktualizacji Windows Update.
Skan odczytuje identyfikator urządzenia, identyfikatory sprzętu PCI/PnP, wersję
sterownika, wersję Windows, typ systemu (stacja robocza/serwer) i architekturę.
Odczyt lokalny pochodzi z Windows CIM. Błąd urządzenia nie jest dowodem istnienia
nowszego sterownika.

## Pokrycie

- NVIDIA Display: publiczny katalog NVIDIA jest odpytywany o rzeczywisty hex
  `VEN_10DE/DEV_XXXX` oraz Windows 10/11 x64. Dodatkowo wymagane są dokładna nazwa
  modelu na liście produktów odpowiedzi, właściwy system, WHQL, DCH i kanał
  Game Ready. Brak dopasowania oznacza wynik niepotwierdzony.
- AMD i Intel: obecnie brak zintegrowanego katalogu z potwierdzonym dopasowaniem
  wszystkich urządzeń. Aplikacja pokazuje ten brak oraz oficjalną stronę
  autowykrywania producenta. Sam link nie oznacza wykonanej weryfikacji.
- Pozostałe urządzenia: aktualność niepotwierdzona; należy użyć wsparcia
  producenta urządzenia/komputera. Nie zgadujemy adresu pakietu ani modelu płyty.

Identyfikatory OS `135` (Windows 11) i `57` (Windows 10 x64) są kluczami katalogu,
nie wpisanymi na sztywno wersjami sterowników. Wersja najnowszego pakietu i data
wydania zawsze pochodzą z odpowiedzi sieciowej. Windows Server, ARM64 i nieznany
system nie są automatycznie traktowane jak obsługiwany Windows x64.

Wynik porównania dotyczy wyłącznie sprawdzonego kanału. Nie obejmuje automatycznie
Studio, sterowników OEM, firmware ani BIOS. Identyfikator subsystem OEM jest
odczytywany, ale nie ma osobnej gwarancji dopasowania OEM w katalogu Game Ready;
instalator producenta jest ostatnim etapem kontroli zgodności. Aplikacja nie
pobiera i nie uruchamia pakietów w tle.

## Źródła producentów

- [NVIDIA Drivers](https://www.nvidia.com/en-us/drivers/)
- [NVIDIA publiczny endpoint katalogu](https://gfwsl.geforce.com/services_toolkit/services/com/nvidia/services/AjaxDriverService.php)
- [AMD Drivers and Support](https://www.amd.com/en/support/download/drivers.html)
- [Intel Driver & Support Assistant](https://www.intel.com/content/www/us/en/support/detect.html)

Endpoint NVIDIA jest interfejsem witryny producenta, nie wersjonowanym API z
gwarancją stabilności. Zmiana formatu, błąd sieci lub brak danych powoduje jawny
wynik błędu/niedostępności; nigdy zielony status aktualności.

## Weryfikacja

`dotnet run --project tests/Aetherial.DriverTests` uruchamia izolowane regresje:
porównanie numerów, zgodność PCI i OS, rozróżnienie Ti/SUPER, błędne źródła,
brak danych, sieć, anulowanie i jawne ograniczenia dostawców.
Opcjonalne `-- --live` sprawdza rzeczywisty katalog NVIDIA na jawnej testowej
konfiguracji RTX 4070 Ti SUPER / Windows 11 x64. Fixture nie jest odczytem
sprzętu użytkownika i nie jest używana przez aplikację.
