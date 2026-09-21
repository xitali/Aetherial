using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using DiskOptimizer.Models;

namespace DiskOptimizer.Services;

public class SoftwareInstallerService
{
    public List<AppPackageItem> GetCuratedCatalog()
    {
        var catalog = new List<AppPackageItem>
        {
            // ==================== 1. SERWER DOMOWY & HOMELAB (22) ====================
            new AppPackageItem { Id = "Tailscale.Tailscale", Name = "Tailscale Mesh VPN", Category = "Serwer Domowy", Icon = "🌐", Description = "Prywatna, szyfrowana sieć mesh VPN do łączenia z domowym serwerem z całego świata bez publicznego IP i bez otwierania portów." },
            new AppPackageItem { Id = "WireGuard.WireGuard", Name = "WireGuard VPN", Category = "Serwer Domowy", Icon = "🔒", Description = "Ultraszybki, nowoczesny tunel kryptograficzny VPN do bezpiecznego dostępu do domowej sieci LAN." },
            new AppPackageItem { Id = "Cloudflare.cloudflared", Name = "Cloudflare Tunnel", Category = "Serwer Domowy", Icon = "☁️", Description = "Bezpieczne wystawianie serwisów domowych (NAS, Nextcloud, Home Assistant) do Internetu przez Cloudflare Zero Trust bez ryzyka ataków na IP." },
            new AppPackageItem { Id = "Plex.PlexMediaServer", Name = "Plex Media Server", Category = "Serwer Domowy", Icon = "🎬", Description = "Komercyjny standard domowego serwera multimediów 4K HDR z transkodowaniem sprzętowym GPU i aplikacjami na TV/telefon." },
            new AppPackageItem { Id = "Jellyfin.Server", Name = "Jellyfin Server", Category = "Serwer Domowy", Icon = "🍿", Description = "W 100% bezpłatny, otwartoźródłowy serwer filmów, seriali i muzyki bez żadnych subskrypcji ani telemetrii." },
            new AppPackageItem { Id = "Jellyfin.JellyfinMediaPlayer", Name = "Jellyfin Media Player", Category = "Serwer Domowy", Icon = "📺", Description = "Zoptymalizowany klient odtwarzacza Jellyfin z natywnym dekodowaniem Direct Play bez obciążania procesora serwera." },
            new AppPackageItem { Id = "Nextcloud.NextcloudDesktop", Name = "Nextcloud Desktop", Category = "Serwer Domowy", Icon = "☁️", Description = "Prywatna chmura danych, plików, kontaktów i kalendarza na własnym serwerze (pełny zamiennik Google Drive i OneDrive)." },
            new AppPackageItem { Id = "Syncthing.Syncthing", Name = "Syncthing P2P", Category = "Serwer Domowy", Icon = "🔄", Description = "Ciągła, zdecentralizowana synchronizacja plików P2P między wszystkimi komputerami a serwerem z pełnym szyfrowaniem." },
            new AppPackageItem { Id = "qBittorrent.qBittorrent", Name = "qBittorrent NAS WebUI", Category = "Serwer Domowy", Icon = "📥", Description = "Wiodący klient torrent z wbudowanym serwerem Web UI, idealny do zdalnego zarządzania pobieraniem na domowy dysk NAS." },
            new AppPackageItem { Id = "Balena.Etcher", Name = "balenaEtcher", Category = "Serwer Domowy", Icon = "💾", Description = "Niezawodne nagrywanie obrazów ISO/IMG systemów Proxmox VE, TrueNAS SCALE, Unraid i CasaOS na pamięci flash i dyski USB." },
            new AppPackageItem { Id = "RaspberryPiFoundation.RaspberryPiImager", Name = "Raspberry Pi Imager", Category = "Serwer Domowy", Icon = "🍓", Description = "Wgrywanie systemów na miniserwery ARM: Raspberry Pi OS, Home Assistant OS, Ubuntu Server z automatyczną konfiguracją SSH i Wi-Fi." },
            new AppPackageItem { Id = "TimKosse.FileZilla.Server", Name = "FileZilla Server", Category = "Serwer Domowy", Icon = "📁", Description = "Własny serwer FTP/FTPS do udostępniania i szybkiego przesyłania plików w domowej sieci lokalnej." },
            new AppPackageItem { Id = "TimKosse.FileZilla.Client", Name = "FileZilla Client", Category = "Serwer Domowy", Icon = "📂", Description = "Klient transferu plików przez SFTP/FTP do zarządzania zawartością dysków serwera." },
            new AppPackageItem { Id = "SimonTatham.PuTTY", Name = "PuTTY SSH", Category = "Serwer Domowy", Icon = "💻", Description = "Klasyczny, lekki klient protokołów SSH i Telnet do zdalnej administracji konsolą linuksową serwera." },
            new AppPackageItem { Id = "MartinPrikryl.WinSCP", Name = "WinSCP SFTP", Category = "Serwer Domowy", Icon = "🔐", Description = "Graficzny eksplorator plików przez SFTP i SCP z wbudowanym edytorem plików konfiguracyjnych serwera (nginx, docker-compose)." },
            new AppPackageItem { Id = "RedHat.Podman-Desktop", Name = "Podman Desktop", Category = "Serwer Domowy", Icon = "🦭", Description = "Lekkie, bezdaemonowe zarządzanie kontenerami OCI i Kubernetesem pod środowiska serwerowe bez opłat licencyjnych." },
            new AppPackageItem { Id = "AdGuard.AdGuardHome", Name = "AdGuard Home DNS", Category = "Serwer Domowy", Icon = "🛡️", Description = "Domowy serwer DNS blokujący reklamy, moduły śledzące, phishing i malware na wszystkich urządzeniach w całej sieci domowej." },
            new AppPackageItem { Id = "CaddyServer.Caddy", Name = "Caddy Server", Category = "Serwer Domowy", Icon = "🌐", Description = "Błyskawiczny serwer WWW i Reverse Proxy z wbudowaną automatyczną obsługą darmowych certyfikatów SSL/TLS Let's Encrypt." },
            new AppPackageItem { Id = "WiresharkFoundation.Wireshark", Name = "Wireshark Analyzer", Category = "Serwer Domowy", Icon = "🦈", Description = "Branżowy analizator pakietów sieciowych do diagnozy problemów z routingiem, przepustowością i bezpieczeństwem serwera." },
            new AppPackageItem { Id = "OpenVPNTechnologies.OpenVPNConnect", Name = "OpenVPN Connect", Category = "Serwer Domowy", Icon = "🔑", Description = "Oficjalny klient tunelowania sieciowego do łączenia się ze zdalnym serwerem OpenVPN." },
            new AppPackageItem { Id = "Derailed.k9s", Name = "k9s Kubernetes CLI", Category = "Serwer Domowy", Icon = "☸️", Description = "Terminalowy interfejs do monitorowania i zarządzania klastrami kontenerów Kubernetes na domowym klastrze serwerów." },
            new AppPackageItem { Id = "JesseDuffield.lazygit", Name = "Lazygit", Category = "Serwer Domowy", Icon = "🌿", Description = "Wygodny terminalowy interfejs TUI do zarządzania repozytoriami Git na serwerach i stacjach roboczych." },

            // ==================== 2. AI & ANTIGRAVITY (14) ====================
            new AppPackageItem { Id = "Google.Antigravity", Name = "Google Antigravity 2.0", Category = "AI", Icon = "🌌", Description = "Autonomiczne środowisko agentowe AI DeepMind do analizy kodu, badań, orchestracji subagentów i zaawansowanego programowania agentowego." },
            new AppPackageItem { Id = "9PLM9XGG6VKS", Name = "ChatGPT dla Windows", Category = "AI", Icon = "💬", Description = "Oficjalna aplikacja OpenAI ChatGPT dla Windows ze skrótem Alt+Space, asystentem programistycznym Codex i integracją z pulpitem." },
            new AppPackageItem { Id = "j178.ChatGPT", Name = "ChatGPT CLI", Category = "AI", Icon = "⌨️", Description = "Szybki, lekki klient terminalowy ChatGPT wspierający najnowsze modele GPT-4o i o1/o3 z obsługą potoków." },
            new AppPackageItem { Id = "Ollama.Ollama", Name = "Ollama AI", Category = "AI", Icon = "🦙", Description = "Lokalny silnik uruchamiania modeli open-source (Llama 3, DeepSeek-R1, Qwen, Mistral) bezpośrednio na Twojej karcie graficznej GeForce RTX." },
            new AppPackageItem { Id = "ElementLabs.LMStudio", Name = "LM Studio", Category = "AI", Icon = "🧠", Description = "Nowoczesne graficzne studio do pobierania, ewaluacji i czatowania z lokalnymi modelami językowymi w formacie GGUF z akceleracją GPU." },
            new AppPackageItem { Id = "Jan.Jan", Name = "Jan AI (100% Offline)", Category = "AI", Icon = "🤖", Description = "Prywatny, w 100% lokalny otwartoźródłowy asystent AI działający w całości offline na Twoim komputerze bez wysyłania danych do chmury." },
            new AppPackageItem { Id = "kangfenmao.CherryStudio", Name = "Cherry Studio", Category = "AI", Icon = "🍒", Description = "Wieloplatformowy desktopowy klient sztucznej inteligencji obsługujący API Claude 3.5, OpenAI, Gemini 2.0 i DeepSeek." },
            new AppPackageItem { Id = "Anysphere.Cursor", Name = "Cursor AI", Category = "AI", Icon = "⚡", Description = "Wiodące środowisko programistyczne zintegrowane z modelami AI potrafiącymi pisać, refaktoryzować i debugować całe repozytoria." },
            new AppPackageItem { Id = "Codeium.Windsurf", Name = "Windsurf IDE", Category = "AI", Icon = "🏄", Description = "Środowisko programistyczne AI z innowacyjnymi przepływami agentowymi Cascade, asystujące przy skomplikowanych architekturach." },
            new AppPackageItem { Id = "ByteDance.Trae", Name = "Trae IDE", Category = "AI", Icon = "🚀", Description = "Inteligentne środowisko programistyczne nowej generacji z autonomicznymi agentami generującymi kod i testy jednostkowe." },
            new AppPackageItem { Id = "Anaconda.Miniconda3", Name = "Miniconda 3", Category = "AI", Icon = "🐍", Description = "Minimalna dystrybucja środowiska Conda dla naukowców danych, inżynierów AI i programistów Pythona." },
            new AppPackageItem { Id = "GitHub.cli", Name = "GitHub CLI (gh)", Category = "AI", Icon = "🐙", Description = "Oficjalne narzędzie wiersza poleceń do obsługi GitHuba, Pull Requestów, wydań i zapytań API." },
            new AppPackageItem { Id = "dbeaver.dbeaver", Name = "DBeaver Community", Category = "AI", Icon = "🦫", Description = "Uniwersalne środowisko do baz danych SQL i NoSQL z asystentami zapytań (PostgreSQL, MySQL, SQLite, Oracle)." },
            new AppPackageItem { Id = "Redis.RedisInsight", Name = "Redis Insight", Category = "AI", Icon = "⚡", Description = "Wizualne narzędzie do monitorowania, analizy kluczy i optymalizacji pamięci podręcznej bazy Redis." },

            // ==================== 3. PROGRAMOWANIE & DEV (22) ====================
            new AppPackageItem { Id = "Microsoft.VisualStudioCode", Name = "Visual Studio Code", Category = "Programowanie", Icon = "💻", Description = "Najpopularniejszy na świecie edytor kodu z gigantycznym ekosystemem rozszerzeń, debuggerów i profili." },
            new AppPackageItem { Id = "Microsoft.VisualStudio.2022.Community", Name = "Visual Studio 2022 Community", Category = "Programowanie", Icon = "🔷", Description = "Pełne środowisko programistyczne IDE dla platformy .NET, języka C#, C++, gier DirectX i aplikacji chmurowych." },
            new AppPackageItem { Id = "Microsoft.WSL", Name = "Windows Subsystem for Linux (WSL)", Category = "Programowanie", Icon = "🐧", Description = "Natywne środowisko Linux (Ubuntu, Debian) wewnątrz Windows 11 bez narzutu wirtualizacji." },
            new AppPackageItem { Id = "Microsoft.WindowsTerminal", Name = "Windows Terminal", Category = "Programowanie", Icon = "⌨️", Description = "Wielokartowa konsola dla PowerShell, CMD i WSL z akceleracją GPU, obsługą motywów i kart." },
            new AppPackageItem { Id = "Git.Git", Name = "Git", Category = "Programowanie", Icon = "🌿", Description = "Wzorcowy rozproszony system kontroli wersji plików i kodu źródłowego." },
            new AppPackageItem { Id = "GitHub.GitHubDesktop", Name = "GitHub Desktop", Category = "Programowanie", Icon = "🐙", Description = "Przejrzysty interfejs graficzny do zarządzania gałęziami, zatwierdzeniami i synchronizacją z GitHubem." },
            new AppPackageItem { Id = "Python.Python.3.12", Name = "Python 3.12", Category = "Programowanie", Icon = "🐍", Description = "Nowoczesny interpreter języka Python wraz z menedżerem pakietów pip i bibliotekami standardowymi." },
            new AppPackageItem { Id = "OpenJS.NodeJS.LTS", Name = "Node.js LTS", Category = "Programowanie", Icon = "🟢", Description = "Stabilne środowisko uruchomieniowe JavaScript po stronie serwera wraz z menedżerem npm." },
            new AppPackageItem { Id = "Rustlang.Rustup", Name = "Rustup (Rust Toolchain)", Category = "Programowanie", Icon = "🦀", Description = "Oficjalny instalator kompilatora i menedżera pakietów Cargo dla nowoczesnego, ultraszybkiego języka Rust." },
            new AppPackageItem { Id = "GoLang.Go", Name = "Go Programming Language", Category = "Programowanie", Icon = "🐹", Description = "Kompilator języka Go (Golang) firmy Google, stworzony do budowy wysoce skalowalnych mikrousług i narzędzi chmurowych." },
            new AppPackageItem { Id = "Docker.DockerDesktop", Name = "Docker Desktop", Category = "Programowanie", Icon = "🐳", Description = "Kompletne środowisko do uruchamiania, budowania i zarządzania kontenerami Linux na systemie Windows." },
            new AppPackageItem { Id = "JetBrains.Toolbox", Name = "JetBrains Toolbox", Category = "Programowanie", Icon = "🧰", Description = "Panel zarządzania i auto-aktualizacji narzędzi IntelliJ IDEA, PyCharm, WebStorm, Rider, GoLand." },
            new AppPackageItem { Id = "Postman.Postman", Name = "Postman", Category = "Programowanie", Icon = "🚀", Description = "Kompleksowa platforma do projektowania, wysyłania zapytań, mockowania i testowania API REST, GraphQL i gRPC." },
            new AppPackageItem { Id = "Kong.Insomnia", Name = "Insomnia REST Client", Category = "Programowanie", Icon = "🌙", Description = "Elegancki i szybki klient API REST, GraphQL i gRPC z obsługą zmiennych środowiskowych i certyfikatów." },
            new AppPackageItem { Id = "SublimeHQ.SublimeText.4", Name = "Sublime Text 4", Category = "Programowanie", Icon = "📝", Description = "Ultraszybki, lekki edytor tekstu i kodu z natywną wydajnością i zaawansowanym wyszukiwaniem." },
            new AppPackageItem { Id = "Notepad++.Notepad++", Name = "Notepad++", Category = "Programowanie", Icon = "📄", Description = "Legendarny, zwinny edytor tekstu z kolorowaniem składni, wtyczkami i obsługą setek formatów." },
            new AppPackageItem { Id = "Neovim.Neovim", Name = "Neovim", Category = "Programowanie", Icon = "⌨️", Description = "Nowoczesna, rozszerzalna wersja edytora Vim z obsługą protokołu LSP i konfiguracji w języku Lua." },
            new AppPackageItem { Id = "DenoLand.Deno", Name = "Deno Runtime", Category = "Programowanie", Icon = "🦕", Description = "Bezpieczne, nowoczesne środowisko dla JavaScript i TypeScript z wbudowanym linterem i formatterem." },
            new AppPackageItem { Id = "Oven-sh.Bun", Name = "Bun Runtime", Category = "Programowanie", Icon = "🥟", Description = "Niewiarygodnie szybkie, kompleksowe środowisko JavaScript z wbudowanym bundlerem, menedżerem i runnerem testów." },
            new AppPackageItem { Id = "Kitware.CMake", Name = "CMake", Category = "Programowanie", Icon = "🔧", Description = "Międzyplatformowe narzędzie do zarządzania procesem kompilacji i budowy projektów C i C++." },
            new AppPackageItem { Id = "BeekeeperStudio.BeekeeperStudio", Name = "Beekeeper Studio", Category = "Programowanie", Icon = "🐝", Description = "Nowoczesny, przejrzysty klient baz danych SQL (PostgreSQL, MySQL, SQLite, SQL Server)." },
            new AppPackageItem { Id = "amacneil.dbmate", Name = "dbmate", Category = "Programowanie", Icon = "📦", Description = "Lekkie narzędzie wiersza poleceń do zarządzania migracjami schematów baz danych bez względu na język projektu." },

            // ==================== 4. GRAFIKA, WIDEO & TWÓRCZOŚĆ (16) ====================
            new AppPackageItem { Id = "Figma.Figma", Name = "Figma Desktop", Category = "Grafika", Icon = "🎨", Description = "Standard branżowy projektowania interfejsów UI/UX, makiet mobilnych i systemów designu z pracą w zespole." },
            new AppPackageItem { Id = "BlenderFoundation.Blender", Name = "Blender 3D", Category = "Grafika", Icon = "🧊", Description = "Światowej klasy pakiet do modelowania 3D, teksturowania, animacji, symulacji fizyki i renderingu Cycles." },
            new AppPackageItem { Id = "BlackmagicDesign.DaVinciResolve", Name = "DaVinci Resolve", Category = "Grafika", Icon = "🎞️", Description = "Profesjonalny montaż wideo, zaawansowany color grading, postprodukcja audio Fairlight i efekty Fusion." },
            new AppPackageItem { Id = "OBSProject.OBSStudio", Name = "OBS Studio", Category = "Grafika", Icon = "🎥", Description = "Najpopularniejsze narzędzie do przechwytywania ekranu w 4K/60fps, nagrywania gameplayów i transmisji na żywo." },
            new AppPackageItem { Id = "KDE.Krita", Name = "Krita Digital Painting", Category = "Grafika", Icon = "🖌️", Description = "Znakomity program dla ilustratorów, malarzy cyfrowych i twórców komiksów z zaawansowaną dynamiką pędzli." },
            new AppPackageItem { Id = "Inkscape.Inkscape", Name = "Inkscape Vector Graphics", Category = "Grafika", Icon = "✒️", Description = "Zaawansowany edytor grafiki wektorowej SVG dla projektantów logo, ikon, infografik i ilustracji." },
            new AppPackageItem { Id = "GIMP.GIMP", Name = "GIMP Image Editor", Category = "Grafika", Icon = "🎨", Description = "Rozbudowane, darmowe środowisko do manipulacji fotografiami, retuszu i edycji grafiki rastrowej." },
            new AppPackageItem { Id = "dotPDN.PaintDotNet", Name = "Paint.NET", Category = "Grafika", Icon = "🖼️", Description = "Szybki edytor zdjęć z obsługą warstw, filtrów i efektów specjalnych, idealny do codziennych zadań graficznych." },
            new AppPackageItem { Id = "Upscayl.Upscayl", Name = "Upscayl AI Upscaler", Category = "Grafika", Icon = "✨", Description = "Sztuczna inteligencja podnosząca rozdzielczość grafik i zdjęć (AI Image Upscaler) z wykorzystaniem mocy karty RTX." },
            new AppPackageItem { Id = "NickeManarin.ScreenToGif", Name = "ScreenToGif", Category = "Grafika", Icon = "🎬", Description = "Niezastąpione narzędzie do nagrywania wybranego fragmentu ekranu i natychmiastowej edycji klatka po klatce do formatu GIF/MP4." },
            new AppPackageItem { Id = "HandBrake.HandBrake", Name = "HandBrake Video Encoder", Category = "Grafika", Icon = "📼", Description = "Uniwersalny konwerter wideo z akceleracją NVENC wspierający kompresję do formatów AV1, HEVC i H.264." },
            new AppPackageItem { Id = "Audacity.Audacity", Name = "Audacity Audio Editor", Category = "Grafika", Icon = "🎙️", Description = "Wielomikrofonowy edytor audio, usuwanie szumów, masterowanie nagrań lektorskich i podcastów." },
            new AppPackageItem { Id = "ShareX.ShareX", Name = "ShareX Screen Capture", Category = "Grafika", Icon = "📸", Description = "Potężny program do robienia zrzutów ekranu, adnotacji, próbkowania kolorów, linijki i automatycznego eksportu." },
            new AppPackageItem { Id = "Meltytech.Shotcut", Name = "Shotcut Video Editor", Category = "Grafika", Icon = "✂️", Description = "Lekki, wielościeżkowy edytor wideo obsługujący setki kodeków bez konieczności importowania plików." },
            new AppPackageItem { Id = "Darktable.Darktable", Name = "Darktable Photo Studio", Category = "Grafika", Icon = "📷", Description = "Cyfrowa ciemnia fotograficzna dla profesjonalistów – bezstratne wywoływanie i obróbka plików RAW z aparatów." },
            new AppPackageItem { Id = "RawTherapee.RawTherapee", Name = "RawTherapee RAW Developer", Category = "Grafika", Icon = "🔍", Description = "Zaawansowany procesor przetwarzania zdjęć RAW z algorytmami demozaikowania i korekty obiektywów." },

            // ==================== 5. SYSTEM IMPROVEMENT & TWEAK (20) ====================
            new AppPackageItem { Id = "Rem0o.FanControl", Name = "Fan Control", Category = "Systemowe", Icon = "💨", Description = "Najbardziej precyzyjne narzędzie do tworzenia własnych krzywych wentylatorów obudowy, chłodzenia CPU i karty graficznej." },
            new AppPackageItem { Id = "AntibodySoftware.WizTree", Name = "WizTree Disk Space", Category = "Systemowe", Icon = "⚡", Description = "Błyskawiczny analizator zajętości dysków odczytujący bezpośrednio tabelę NTFS MFT w ułamku sekundy." },
            new AppPackageItem { Id = "JAMSoftware.TreeSize.Free", Name = "TreeSize Free", Category = "Systemowe", Icon = "🌳", Description = "Graficzna wizualizacja rozmiarów katalogów, drzewa podfolderów i marnotrawstwa przestrzeni dyskowej." },
            new AppPackageItem { Id = "RevoUninstaller.RevoUninstaller", Name = "Revo Uninstaller", Category = "Systemowe", Icon = "🗑️", Description = "Gruntowne usuwanie programów wraz z czyszczeniem pozostałości w rejestrze i ukrytych folderach AppData." },
            new AppPackageItem { Id = "Microsoft.Sysinternals.Suite", Name = "Sysinternals Suite", Category = "Systemowe", Icon = "🛠️", Description = "Oficjalny pakiet narzędzi Microsoftu dla ekspertów (Process Explorer, Autoruns, TCPView, RAMMap, ProcMon)." },
            new AppPackageItem { Id = "File-New-Project.EarTrumpet", Name = "EarTrumpet Volume Mixer", Category = "Systemowe", Icon = "🔊", Description = "Nowoczesny mikser głośności Windows z indywidualną regulacją głośności dla każdej uruchomionej aplikacji." },
            new AppPackageItem { Id = "QL-Win.QuickLook", Name = "QuickLook Preview", Category = "Systemowe", Icon = "👁️", Description = "Błyskawiczny podgląd plików (zdjęć, PDF, wideo, archiwów) w Eksploratorze po wciśnięciu klawisza Spacja." },
            new AppPackageItem { Id = "voidtools.Everything", Name = "Everything Search", Category = "Systemowe", Icon = "🔍", Description = "Ekspresowa wyszukiwarka plików na wszystkich dyskach NTFS reagująca w czasie rzeczywistym podczas pisania." },
            new AppPackageItem { Id = "7zip.7zip", Name = "7-Zip Archiwizator", Category = "Systemowe", Icon = "📦", Description = "Standard branżowy kompresji danych z maksymalnym stopniem upakowania 7z i obsługą haseł AES-256." },
            new AppPackageItem { Id = "RARLab.WinRAR", Name = "WinRAR", Category = "Systemowe", Icon = "📚", Description = "Legendarny archiwizator formatów RAR i ZIP z funkcją naprawy uszkodzonych archiwów." },
            new AppPackageItem { Id = "CrystalDewWorld.CrystalDiskInfo", Name = "CrystalDiskInfo S.M.A.R.T.", Category = "Systemowe", Icon = "💽", Description = "Monitorowanie parametrów S.M.A.R.T. dysków SSD NVMe/SATA, temperatur oraz wskaźnika zużycia komórek pamięci." },
            new AppPackageItem { Id = "CrystalDewWorld.CrystalDiskMark", Name = "CrystalDiskMark Benchmark", Category = "Systemowe", Icon = "🚀", Description = "Precyzyjny benchmark prędkości odczytu i zapisu losowego (IOPS) oraz sekwencyjnego dla dysków SSD." },
            new AppPackageItem { Id = "CPUID.CPU-Z", Name = "CPU-Z Hardware Info", Category = "Systemowe", Icon = "🧠", Description = "Szczegółowa telemetria architektury procesora, pamięci RAM DDR5, timingów i specyfikacji płyty głównej." },
            new AppPackageItem { Id = "TechPowerUp.GPU-Z", Name = "GPU-Z Graphics Telemetry", Category = "Systemowe", Icon = "🖥️", Description = "Precyzyjny podgląd taktowania GPU, pamięci VRAM, magistrali PCIe, wersji BIOS i czujników temperatur." },
            new AppPackageItem { Id = "CPUID.HWMonitor", Name = "HWMonitor Sensors", Category = "Systemowe", Icon = "🌡️", Description = "Ciągły monitoring napięć zasilania, temperatur rdzeni procesora, wentylatorów i poboru mocy (W)." },
            new AppPackageItem { Id = "REALiX.HWiNFO", Name = "HWiNFO Diagnostics", Category = "Systemowe", Icon = "📊", Description = "Najbardziej zaawansowane narzędzie diagnostyki sprzętowej ze wsparciem dla czujników stacji roboczych i logowania." },
            new AppPackageItem { Id = "WinsiderSeminars.SystemInformer", Name = "System Informer", Category = "Systemowe", Icon = "⚙️", Description = "Zaawansowany zamiennik Menedżera Zadań do analizy wątków, zależności DLL, otwartych uchwytów i sieci." },
            new AppPackageItem { Id = "Rufus.Rufus", Name = "Rufus USB Creator", Category = "Systemowe", Icon = "💾", Description = "Tworzenie bootowalnych nośników instalacyjnych USB z możliwością pominięcia wymogów TPM/RAM dla Windows 11." },
            new AppPackageItem { Id = "Microsoft.PowerToys", Name = "Microsoft PowerToys", Category = "Systemowe", Icon = "🧰", Description = "Przydatne ułatwienia systemowe: strefy okien FancyZones, ColorPicker, Text Extractor OCR i PowerToys Run." },
            new AppPackageItem { Id = "AutoHotkey.AutoHotkey", Name = "AutoHotkey Automation", Category = "Systemowe", Icon = "⌨️", Description = "Język skryptowy do automatyzacji powtarzalnych czynności, przypisywania makr i własnych skrótów klawiaturowych." },

            // ==================== 6. PRZEGLĄDARKI INTERNETOWE (10) ====================
            new AppPackageItem { Id = "Google.Chrome", Name = "Google Chrome", Category = "Przeglądarki", Icon = "🌐", Description = "Najpopularniejsza przeglądarka internetowa z synchronizacją konta Google." },
            new AppPackageItem { Id = "Brave.Brave", Name = "Brave Browser", Category = "Przeglądarki", Icon = "🦁", Description = "Błyskawiczna przeglądarka z wbudowaną blokadą reklam i skryptów śledzących." },
            new AppPackageItem { Id = "Mozilla.Firefox", Name = "Mozilla Firefox", Category = "Przeglądarki", Icon = "🦊", Description = "Otwarta, niezależna przeglądarka z zaawansowaną ochroną prywatności." },
            new AppPackageItem { Id = "Microsoft.Edge", Name = "Microsoft Edge", Category = "Przeglądarki", Icon = "🌊", Description = "Zoptymalizowana dla Windows 11 przeglądarka z asystentem Copilot." },
            new AppPackageItem { Id = "Opera.Opera", Name = "Opera", Category = "Przeglądarki", Icon = "🔴", Description = "Przeglądarka z darmowym VPN, blokadą trackerów i paskiem komunikatorów." },
            new AppPackageItem { Id = "Opera.OperaGX", Name = "Opera GX Gaming", Category = "Przeglądarki", Icon = "🎮", Description = "Przeglądarka gamingowa z kontrolerem zużycia procesora i pamięci RAM." },
            new AppPackageItem { Id = "Vivaldi.Vivaldi", Name = "Vivaldi", Category = "Przeglądarki", Icon = "🧭", Description = "Maksymalnie konfigurowalna przeglądarka z panelami wielozadaniowości." },
            new AppPackageItem { Id = "TorProject.TorBrowser", Name = "Tor Browser", Category = "Przeglądarki", Icon = "🧅", Description = "Anonimowe przeglądanie sieci zabezpieczone routingiem cebulowym." },
            new AppPackageItem { Id = "LibreWolf.LibreWolf", Name = "LibreWolf", Category = "Przeglądarki", Icon = "🐺", Description = "Odmiana Firefoksa nastawiona na 100% prywatności bez telemetrii." },
            new AppPackageItem { Id = "Waterfox.Waterfox", Name = "Waterfox", Category = "Przeglądarki", Icon = "💧", Description = "Wysokowydajna 64-bitowa przeglądarka z obsługą tradycyjnych rozszerzeń." },

            // ==================== 7. KOMUNIKACJA & SPOŁECZNOŚĆ (10) ====================
            new AppPackageItem { Id = "Discord.Discord", Name = "Discord", Category = "Komunikacja", Icon = "💬", Description = "Komunikator głosowy, wideo i czat tekstowy dla graczy i społeczności." },
            new AppPackageItem { Id = "Telegram.TelegramDesktop", Name = "Telegram Desktop", Category = "Komunikacja", Icon = "✈️", Description = "Szybki, bezpieczny komunikator z chmurą i przesyłaniem dużych plików." },
            new AppPackageItem { Id = "WhatsApp.WhatsApp", Name = "WhatsApp", Category = "Komunikacja", Icon = "📱", Description = "Oficjalna aplikacja WhatsApp z szyfrowaniem wiadomości end-to-end." },
            new AppPackageItem { Id = "SlackTechnologies.Slack", Name = "Slack", Category = "Komunikacja", Icon = "💼", Description = "Główna platforma komunikacji firmowej i zarządzania projektami." },
            new AppPackageItem { Id = "Zoom.Zoom", Name = "Zoom Workplace", Category = "Komunikacja", Icon = "📹", Description = "Niezawodne wideokonferencje, spotkania online i udostępnianie ekranu." },
            new AppPackageItem { Id = "Microsoft.Teams", Name = "Microsoft Teams", Category = "Komunikacja", Icon = "👥", Description = "Komunikator biznesowy i edukacyjny w ekosystemie Microsoft 365." },
            new AppPackageItem { Id = "Signal.Signal", Name = "Signal Private Messenger", Category = "Komunikacja", Icon = "🔒", Description = "Maksymalnie bezpieczny komunikator bez reklam i gromadzenia metadanych." },
            new AppPackageItem { Id = "Element.Element", Name = "Element Matrix Messenger", Category = "Komunikacja", Icon = "🌐", Description = "Zdecentralizowany komunikator oparty na bezpiecznym protokole Matrix." },
            new AppPackageItem { Id = "Mozilla.Thunderbird", Name = "Mozilla Thunderbird", Category = "Komunikacja", Icon = "📬", Description = "Zaawansowany klient poczty e-mail, kalendarzy i kanałów RSS." },
            new AppPackageItem { Id = "Viber.Viber", Name = "Viber", Category = "Komunikacja", Icon = "📞", Description = "Darmowe połączenia głosowe, wideorozmowy i komunikator grupowy." },

            // ==================== 8. GRY & ROZRYWKA (10) ====================
            new AppPackageItem { Id = "Valve.Steam", Name = "Steam", Category = "Gry", Icon = "🎮", Description = "Największy na świecie sklep cyfrowy i platforma dla graczy PC." },
            new AppPackageItem { Id = "EpicGames.EpicGamesLauncher", Name = "Epic Games Launcher", Category = "Gry", Icon = "🔥", Description = "Sklep Epic Games z cotygodniowymi darmowymi grami i silnikiem Unreal Engine." },
            new AppPackageItem { Id = "ElectronicArts.EADesktop", Name = "EA App", Category = "Gry", Icon = "⚽", Description = "Oficjalna platforma wydawnicza gier EA (FIFA, Battlefield, Apex Legends)." },
            new AppPackageItem { Id = "Ubisoft.Connect", Name = "Ubisoft Connect", Category = "Gry", Icon = "⚔️", Description = "Platforma gier Ubisoft (Assassin's Creed, Rainbow Six, Far Cry)." },
            new AppPackageItem { Id = "Blizzard.BattleNet", Name = "Battle.net", Category = "Gry", Icon = "🛡️", Description = "Launcher gier Blizzard Entertainment (World of Warcraft, Diablo, Overwatch)." },
            new AppPackageItem { Id = "GOG.Galaxy", Name = "GOG GALAXY", Category = "Gry", Icon = "🪐", Description = "Platforma gier bez DRM łącząca biblioteki ze wszystkich Twoich kont." },
            new AppPackageItem { Id = "HeroicGamesLauncher.HeroicGamesLauncher", Name = "Heroic Launcher", Category = "Gry", Icon = "🦸", Description = "Otwarty, lekki launcher dla kont Epic Games, GOG i Amazon Prime Gaming." },
            new AppPackageItem { Id = "Playnite.Playnite", Name = "Playnite Game Manager", Category = "Gry", Icon = "🕹️", Description = "Uniwersalny menedżer scalający wszystkie gry w jedną piękną bibliotekę." },
            new AppPackageItem { Id = "Guru3D.Afterburner", Name = "MSI Afterburner", Category = "Gry", Icon = "⏱️", Description = "Narzędzie do podkręcania karty graficznej, kontroli krzywej wentylatorów i nakładki FPS." },
            new AppPackageItem { Id = "MoonlightGameStreamingProject.Moonlight", Name = "Moonlight Game Streaming", Category = "Gry", Icon = "🌙", Description = "Błyskawiczne strumieniowanie gier z PC na telewizor, tablet lub telefon." },

            // ==================== 9. BIURO, WIEDZA & BEZPIECZEŃSTWO (16) ====================
            new AppPackageItem { Id = "TheDocumentFoundation.LibreOffice", Name = "LibreOffice Suite", Category = "Biuro", Icon = "📊", Description = "Kompletny darmowy pakiet biurowy zgodny z formatami Microsoft Word/Excel." },
            new AppPackageItem { Id = "Adobe.Acrobat.Reader.64-bit", Name = "Adobe Acrobat Reader", Category = "Biuro", Icon = "📑", Description = "Standardowy program do odczytu, podpisywania i komentowania plików PDF." },
            new AppPackageItem { Id = "Notion.Notion", Name = "Notion Workspace", Category = "Biuro", Icon = "📓", Description = "Elastyczny notes, baza wiedzy i tablica zadań dla zespołów i osób prywatnych." },
            new AppPackageItem { Id = "Obsidian.Obsidian", Name = "Obsidian Knowledge Base", Category = "Biuro", Icon = "💎", Description = "Prywatny lokalny notes Markdown budujący sieć powiązań między myślami." },
            new AppPackageItem { Id = "Foxit.FoxitReader", Name = "Foxit PDF Reader", Category = "Biuro", Icon = "📄", Description = "Lekka, szybka przeglądarka dokumentów PDF z narzędziami adnotacji." },
            new AppPackageItem { Id = "KovidGoyal.Calibre", Name = "Calibre E-book Manager", Category = "Biuro", Icon = "📚", Description = "Wszechstronny menedżer e-booków i konwerter formatów EPUB/MOBI." },
            new AppPackageItem { Id = "AnyDeskSoftwareGmbH.AnyDesk", Name = "AnyDesk Remote Desktop", Category = "Biuro", Icon = "🖥️", Description = "Program do zdalnego pulpitu z płynnym obrazem o niskich opóźnieniach." },
            new AppPackageItem { Id = "TeamViewer.TeamViewer", Name = "TeamViewer", Category = "Biuro", Icon = "🤝", Description = "Uznane na świecie narzędzie do zdalnego wsparcia technicznego i kontroli PC." },
            new AppPackageItem { Id = "Bitwarden.Bitwarden", Name = "Bitwarden Password Vault", Category = "Biuro", Icon = "🛡️", Description = "Renomowany, bezpieczny menedżer haseł z szyfrowaniem end-to-end." },
            new AppPackageItem { Id = "DominikReichl.KeePass", Name = "KeePass Password Safe", Category = "Biuro", Icon = "🔑", Description = "Klasyczny sejf haseł trzymający bazę wyłącznie lokalnie na Twoim dysku." },
            new AppPackageItem { Id = "Proton.ProtonVPN", Name = "Proton VPN", Category = "Biuro", Icon = "🛡️", Description = "Bezpieczny szwajcarski VPN z nielimitowanym transferem i ochroną prywatności." },
            new AppPackageItem { Id = "NordVPN.NordVPN", Name = "NordVPN", Category = "Biuro", Icon = "🌐", Description = "Szybki dostawca sieci VPN z zaawansowaną blokadą zagrożeń i złośliwego oprogramowania." },
            new AppPackageItem { Id = "Cloudflare.1.1.1.1", Name = "Cloudflare WARP", Category = "Biuro", Icon = "⚡", Description = "Zabezpieczone, przyspieszone połączenie internetowe z serwerami DNS 1.1.1.1." },
            new AppPackageItem { Id = "VideoLAN.VLC", Name = "VLC Media Player", Category = "Biuro", Icon = "🎬", Description = "Legendarny odtwarzacz wideo czytający każdy kodek bez konieczności instalowania dodatków." },
            new AppPackageItem { Id = "Spotify.Spotify", Name = "Spotify Music", Category = "Biuro", Icon = "🎵", Description = "Miliony utworów muzycznych, playlist i podcastów w wysokiej jakości na żądanie." },
            new AppPackageItem { Id = "PeterPawlowski.foobar2000", Name = "foobar2000 Audio Player", Category = "Biuro", Icon = "📻", Description = "Audiofilski odtwarzacz muzyki z obsługą bezstratnych formatów FLAC i DSD." }
        };
        foreach (var app in catalog)
        {
            app.Source = !app.Id.Contains('.') && app.Id.Length == 12 ? "msstore" : "winget";
            app.IsPackageIdVerified = SoftwareInventoryParser.IsActionableId(app.Id, app.Source);
        }
        return catalog;
    }

    /// <summary>Read-only inventory of all apps returned by WinGet, with a local registry fallback.</summary>
    public async Task<SoftwareInventoryResult> GetInstalledAppsAsync(Action<string>? logger = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(90));
        logger?.Invoke("Odczytywanie zainstalowanych programów i dostępnych wersji...");
        var (ok, output, _) = await DiskHelper.RunProcessDetailedAsync("winget",
            "list --accept-source-agreements --disable-interactivity", null, timeout.Token);
        // The shared process wrapper returns a failure for cancellation; preserve it here.
        ct.ThrowIfCancellationRequested();
        var parsed = SoftwareInventoryParser.Parse(output);
        if (ok && parsed.HasTable && parsed.Apps.Count > 0 && parsed.RejectedRows == 0)
        {
            string status = $"{parsed.Apps.Count} programów · {parsed.Apps.Count(a => a.HasUpdate)} aktualizacji";
            logger?.Invoke(status);
            return new SoftwareInventoryResult(parsed.Apps, true, true, status);
        }

        var fallback = await Task.Run(() => GetRegistryInventory(ct), ct);
        // Never turn a failed source lookup into an 'everything up to date' result.
        string reason = timeout.IsCancellationRequested ? "Przekroczono czas odczytu WinGet." :
            ok ? "Nie udało się w pełni odczytać formatu odpowiedzi WinGet." : "WinGet jest niedostępny lub nie odczytał źródeł.";
        string message = $"{reason} Lista z rejestru Windows: {fallback.Count}. Aktualizacje niezweryfikowane.";
        logger?.Invoke(message);
        return new SoftwareInventoryResult(fallback, false, false, message);
    }

    public async Task RefreshInstalledStatusesAsync(IEnumerable<AppPackageItem> catalog, string? customInstallFolder = null, Action<string>? logger = null)
    {
        var inventory = await GetInstalledAppsAsync(logger);
        ApplyInventoryToCatalog(catalog, inventory);
    }

    public static void ApplyInventoryToCatalog(IEnumerable<AppPackageItem> catalog, SoftwareInventoryResult inventory)
    {
        var exact = inventory.Apps.Where(a => a.IsPackageIdVerified)
            .GroupBy(a => a.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.HasUpdate).First(), StringComparer.OrdinalIgnoreCase);
        var names = inventory.Apps.GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var app in catalog)
        {
            app.HasUpdate = false;
            app.AvailableVersion = "";
            app.InstalledVersion = "";
            app.UpdatesChecked = false;
            app.IsInstalled = false;
            app.IsSelected = false;
            if (exact.TryGetValue(app.Id, out var installed))
            {
                app.IsInstalled = true;
                app.InstalledVersion = installed.InstalledVersion;
                app.AvailableVersion = installed.AvailableVersion;
                app.HasUpdate = installed.HasUpdate;
                app.UpdatesChecked = installed.UpdatesChecked;
                app.Source = installed.Source;
                app.IsPackageIdVerified = true;
                app.Status = installed.Status;
                app.StatusColor = installed.StatusColor;
            }
            else if (names.TryGetValue(app.Name, out var local))
            {
                // An exact display name establishes presence, never an update package identity.
                app.IsInstalled = true;
                app.InstalledVersion = local.InstalledVersion;
                app.IsPackageIdVerified = false;
                app.Status = "Zainstalowany · aktualizacje niezweryfikowane";
                app.StatusColor = "#888898";
            }
            else
            {
                // Missing from an incomplete inventory says nothing about installation state.
                // Disable installs too, rather than accidentally reinstalling an unknown record.
                app.IsPackageIdVerified = inventory.IsComplete && !app.InventoryOnly &&
                    SoftwareInventoryParser.IsActionableId(app.Id, app.Source);
                app.Status = inventory.IsComplete ? "Niewykryty przez WinGet" : "Stan niezweryfikowany";
                app.StatusColor = "#888898";
            }
        }
    }

    private static List<AppPackageItem> GetRegistryInventory(CancellationToken ct)
    {
        var apps = new List<AppPackageItem>();
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var uninstall = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (uninstall is null) continue;
                foreach (string subName in uninstall.GetSubKeyNames())
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        using var key = uninstall.OpenSubKey(subName);
                        string? name = key?.GetValue("DisplayName") as string;
                        if (string.IsNullOrWhiteSpace(name) || Convert.ToString(key?.GetValue("SystemComponent")) == "1") continue;
                        apps.Add(new AppPackageItem
                        {
                            Id = $"Registry:{hive}:{view}:{subName}", Name = name,
                            InstalledVersion = key?.GetValue("DisplayVersion") as string ?? "",
                            Category = "Zainstalowane", Source = "Rejestr Windows", InventoryOnly = true,
                            IsInstalled = true, IsPackageIdVerified = false,
                            Description = key?.GetValue("Publisher") as string ?? "",
                            Status = "Aktualizacje niezweryfikowane", StatusColor = "#888898"
                        });
                    }
                    catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
                }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
        }
        return apps.DistinctBy(a => $"{a.Name}\u001f{a.InstalledVersion}", StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<bool> InstallAppAsync(AppPackageItem app, string? targetLocation, Action<string>? logger = null, CancellationToken ct = default, bool silent = true)
    {
        ct.ThrowIfCancellationRequested();
        if (!app.CanInstall || !SoftwareInventoryParser.IsActionableId(app.Id, app.Source))
        {
            logger?.Invoke("Instalacja wymaga programu wybranego z katalogu i potwierdzonego identyfikatora pakietu.");
            return false;
        }
        app.IsBusy = true;
        app.Status = "Pobieranie i instalacja...";
        app.StatusColor = "#38BDF8";
        logger?.Invoke($"⬇️ Rozpoczynam automatyczną instalację: {app.Name} ({app.Id})...");

        bool isMsStore = app.Source.Equals("msstore", StringComparison.OrdinalIgnoreCase);
        string args = isMsStore
            ? $"install --id \"{app.Id}\" --source msstore --accept-package-agreements --accept-source-agreements"
            : $"install --id \"{app.Id}\" --source winget --exact {(silent ? "--silent" : "--interactive")} --accept-package-agreements --accept-source-agreements";

        if (!isMsStore && !string.IsNullOrWhiteSpace(targetLocation))
        {
            string sanitizedName = string.Concat(app.Name.Split(Path.GetInvalidFileNameChars())).Trim();
            string appSpecificFolder = Path.Combine(targetLocation, sanitizedName);
            try
            {
                if (!Directory.Exists(appSpecificFolder))
                    Directory.CreateDirectory(appSpecificFolder);
            }
            catch { }

            args += $" --location \"{appSpecificFolder}\"";
            logger?.Invoke($"  📁 Dedykowany podkatalog programu: {appSpecificFolder}");
        }

        (bool ok, string output, int exitCode) result;
        try { result = await DiskHelper.RunProcessDetailedAsync("winget", args, logger, ct); ct.ThrowIfCancellationRequested(); }
        catch (OperationCanceledException) { app.Status = "Anulowano"; throw; }
        finally { app.IsBusy = false; }
        var (ok, output, exitCode) = result;

        app.IsBusy = false;

        // Kod 0 = sukces instalacji
        // Kod -1978335189 (0x8A15002B) = WINGET_INSTALLED_VERSION_ALREADY_NEWEST (Program jest już zainstalowany w najnowszej wersji)
        // Kod -1978335187 (0x8A15002D) = WINGET_NO_APPLICABLE_UPDATE_FOUND
        // Kod -1978335212 (0x8A150014) = WINGET_PACKAGE_ALREADY_INSTALLED
        // Kod -1978335188 (0x8A15002C) = WINGET_UPDATE_NOT_APPLICABLE
        bool isAlreadyUpToDate = exitCode == -1978335189 
            || exitCode == -1978335187 
            || exitCode == -1978335212 
            || exitCode == -1978335188;

        if (ok || isAlreadyUpToDate)
        {
            app.IsInstalled = true;
            app.HasUpdate = false;
            app.AvailableVersion = "";
            app.UpdatesChecked = false;
            app.Status = isAlreadyUpToDate ? "Pakiet jest już zainstalowany" : "Zainstalowano pomyślnie";
            app.StatusColor = "#81C784";
            if (isAlreadyUpToDate)
            {
                logger?.Invoke($"✓ WinGet potwierdził, że {app.Name} jest już zainstalowany.");
            }
            else
            {
                logger?.Invoke($"🎉 Pomyślnie zainstalowano {app.Name}!");
            }
            return true;
        }
        else
        {
            app.Status = "Błąd instalacji";
            app.StatusColor = "#FF5252";
            logger?.Invoke($"❌ Błąd instalacji {app.Name}: Kod zakończenia: {exitCode}");
            return false;
        }
    }

    public async Task<bool> UpgradeAppAsync(AppPackageItem app, Action<string>? logger = null, CancellationToken ct = default, bool silent = true)
    {
        ct.ThrowIfCancellationRequested();
        if (!app.CanUpdate || !SoftwareInventoryParser.IsActionableId(app.Id, app.Source))
        {
            logger?.Invoke("Brak potwierdzonej aktualizacji dla tego pakietu.");
            return false;
        }
        app.IsBusy = true;
        app.Status = "Aktualizowanie...";
        app.StatusColor = "#FFB74D";
        logger?.Invoke($"⬆️ Rozpoczynam aktualizację: {app.Name} ({app.Id})...");

        bool isMsStore = app.Source.Equals("msstore", StringComparison.OrdinalIgnoreCase);
        string args = isMsStore
            ? $"upgrade --id \"{app.Id}\" --source msstore --accept-package-agreements --accept-source-agreements"
            : $"upgrade --id \"{app.Id}\" --source winget --exact {(silent ? "--silent" : "--interactive")} --accept-package-agreements --accept-source-agreements";

        (bool ok, string output, int exitCode) result;
        try { result = await DiskHelper.RunProcessDetailedAsync("winget", args, logger, ct); ct.ThrowIfCancellationRequested(); }
        catch (OperationCanceledException) { app.Status = "Anulowano"; throw; }
        finally { app.IsBusy = false; }
        var (ok, output, exitCode) = result;

        app.IsBusy = false;

        bool isAlreadyUpToDate = exitCode == -1978335189 
            || exitCode == -1978335187 
            || exitCode == -1978335188;

        if (ok || isAlreadyUpToDate)
        {
            app.HasUpdate = false;
            app.AvailableVersion = "";
            app.UpdatesChecked = false;
            app.Status = isAlreadyUpToDate ? "Brak aktualizacji w WinGet" : "Zaktualizowano. Odśwież dane wersji.";
            app.StatusColor = "#81C784";
            if (isAlreadyUpToDate)
            {
                logger?.Invoke($"✓ WinGet nie udostępnia nowszej wersji {app.Name}.");
            }
            else
            {
                logger?.Invoke($"🎉 Pomyślnie zaktualizowano {app.Name}!");
            }
            return true;
        }
        else
        {
            app.Status = "Błąd aktualizacji";
            app.StatusColor = "#FF5252";
            logger?.Invoke($"❌ Błąd aktualizacji {app.Name}: Kod zakończenia: {exitCode}");
            return false;
        }
    }
}
