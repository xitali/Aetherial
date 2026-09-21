# Aetherial 6.3.0

Uproszczony interfejs: jedno boczne menu, jeden pasek tytułu i ekran Czyść na start.
Wyniki, opisy i działania są pokazywane wtedy, gdy są potrzebne.

- Czyść: skan, krótka lista kategorii, szczegóły na żądanie, podgląd i wynik.
- Dyski: wykryte woluminy, duże pliki, lista folderów oraz akcje przy zaznaczeniu.
- Sterowniki: krótkie wiersze; wersje, źródła i identyfikatory w szczegółach.
- Programy: rzeczywiste instalacje, aktualizacje i osobny katalog. Niezweryfikowane
  wpisy nie stają się celami instalacji. Odświeżanie kasuje stare wyniki.
- Narzędzia: cztery kategorie, wybrane zadanie i zawsze dostępny powrót.
- Historia i dziennik dostępne na żądanie; krótsze opisy ustawień.

Porównanie sterowników z katalogiem producenta obejmuje NVIDIA Game Ready WHQL
DCH na Windows 10/11 x64. Inne urządzenia mają jawnie niepotwierdzony status.
Odczyt programów wykorzystuje WinGet; gdy jest niedostępny, aplikacja prezentuje
instalacje z rejestru bez deklarowania aktualności.

Pakiet zastępuje poprzednie wydanie. Dane użytkownika pozostają w LocalAppData.
Automatyczny aktualizator samego Aetherial oraz pozostałe etapy planu są nadal
opisane w BACKLOG.md; to wydanie nie deklaruje ich ukończenia.
