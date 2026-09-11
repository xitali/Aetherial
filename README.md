# AETHERIAL • Storage & Diagnostics Suite

<div align="center">

![Aetherial Banner](assets/app_icon.png)

**Aeronautic Storage Optimization, Hardware Telemetry & Autonomous Diagnostics Suite for Windows 11**

[![Platform](https://img.shields.io/badge/Platform-Windows%2011%20%7C%2010%20(x64)-0078D4?style=for-the-badge&logo=windows)](https://microsoft.com)
[![Framework](https://img.shields.io/badge/.NET-8.0%20WPF%20(AOT%20Single--File)-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com)
[![License](https://img.shields.io/badge/License-MIT-10B981?style=for-the-badge)](LICENSE)
[![Status](https://img.shields.io/badge/Release-v5.6%20Ultra%20Pro-06B6D4?style=for-the-badge)]()

</div>

---

## 🌌 Overview

**AETHERIAL** is a next-generation, aerospace-grade system utility designed for developers, power users, homelab operators, and gamers. Built natively with **C# 12 and .NET 8 WPF**, it combines an ultra-modern cyber-dark dashboard with deep Windows diagnostic automation, driver inventorying, and package ecosystem management.

---

## ✨ Core Features

### ⚡ 1. 1-Click Autonomous Optimization & Cleanup
- **Safe Deep Clean**: Recovers gigabytes of disk space without endangering Windows CBS/DISM servicing databases or component manifests.
- **Smart Target Verification**: Cleans GPU shader caches (DirectX, Vulkan, NVIDIA, AMD), developer environments (Docker, WSL), browsers, and crash dumps only when actually installed on the machine.
- **30-Second Confirmation Shield**: Clear visual confirmation of system peak optimization until new debris accumulates.

### 🎛️ 2. Cyber-Neon Isometric Dashboard
- **3D Isometric Drive Visualizer**: Real-time rendering of all partitions (C:, D:, E:, F:) with gradient capacity columns and diamond tops.
- **Radial Health & Defrag Ring**: Dual-loop 98% SSD health telemetry and instant ReTrim execution.
- **Live System Waveform**: Real-time read/write throughput spline chart and active security state indicators.
- **Hardware Telemetry Gauges**: Circular load meters for CPU (Ryzen 7 7800X3D), GPU (GeForce RTX 4070 Ti SUPER), and RAM (DDR5 6000 EXPO).

### 🖥️ 3. Hardware Diagnostics & Live PnP Inventory
- **6-Point Issue Resolver**: Automatic detection of Code 28 (Missing AMD Sensor Fusion Hub), Code 43 (Bluetooth state), Killer 2.5G network updates, BIOS AGESA suggestions, and SSD ReTrim alerts.
- **Full PnP Inventory**: Categorized hardware overview (GPU, CPU, Sound, Wi-Fi 6E, NVMe, USB controllers) with non-truncated titles and multi-line wrapping.
- **WHQL Driver Updates**: Windows Update WHQL driver catalog checks.

### 📦 4. Software Hub & "Always Up-To-Date" Package Manager
- **Curated 100+ Catalog**: Homelab (Tailscale, WireGuard, Plex, Jellyfin, Nextcloud, Cloudflared), AI (ChatGPT, Antigravity, Ollama, LM Studio, Cursor), Dev (VS Code, Git, Docker, Node.js, Python), Graphics (Blender, DaVinci Resolve, Krita), and System Tools.
- **Winget & AppX Integration**: Instant scanning of registry and AppModel repositories (including Microsoft Store apps like ChatGPT).
- **Custom Install Folder**: Direct installation into dedicated application subfolders (`--location "D:\Programy\<AppName>"`).
- **Silent Bulk Upgrades**: One-click bulk updating of all installed software to the newest stable releases.

### 🔗 5. Power Tools & Developer Cleaners
- **NTFS Junction Symlink Mover**: Transparently migrate heavy folders from drive C: to secondary storage (`mklink /J`) while keeping Windows paths identical.
- **Dev Project Artifact Cleaner**: Safely reclaim tens of gigabytes by pruning stale `node_modules`, `.venv`, and `bin/obj` folders while preserving 100% of source code.
- **System Tuning**: VHDX compaction (WSL2/Docker), CompactOS NTFS compression, and Hibernation file reducer.

---

## 🛠️ Project Structure

```
d:\czyszczenie/
├── Assets/                        # Visual branding & multi-res ICO/PNG assets
│   ├── app_icon.ico               # Multi-resolution Windows application icon
│   └── app_icon.png               # Cyber-delta vector neon glyph
├── Models/                        # Data contracts and observable view models
│   ├── AppPackageItem.cs          # Software hub package definition
│   ├── AppSettings.cs             # Global application configuration
│   ├── CleanItem.cs               # File system cleanup target
│   ├── DriveModel.cs              # Storage partition telemetry
│   └── PnpDeviceItem.cs           # Physical hardware PnP model
├── Services/                      # Application logic and Windows API services
│   ├── CompactOsService.cs        # NTFS CompactOS & VHDX shrink engine
│   ├── DevProjectsService.cs      # Node/Python dev cache scanner
│   ├── DiskCleanerService.cs      # Safe temporary file cleaner
│   ├── DiskHelper.cs              # Process execution & file system utilities
│   ├── DriverUpdaterService.cs    # PnP hardware & WHQL engine
│   ├── MemoryOptimizerService.cs  # Windows RAM working set trimmer
│   ├── SettingsService.cs         # JSON persistent settings manager
│   ├── SoftwareInstallerService.cs# Winget & AppX management
│   └── SymlinkService.cs          # NTFS Junction relocation engine
├── App.xaml                       # Application entry point & cyber themes
├── App.xaml.cs                    # Crash protection & exception handling
├── MainWindow.xaml                # Complete Aetherial Cyber-Neon UI layout
├── MainWindow.xaml.cs             # Event handlers & asynchronous operations
├── DiskOptimizer.csproj           # .NET 8 WPF AOT single-file build definition
├── app.manifest                   # Elevation manifest (requireAdministrator)
├── DiskOptimizer_Manual.html      # Comprehensive interactive user manual
└── DiskOptimizer_Podrecznik_Ekspercki.pdf # 9-page full-bleed manual (PDF)
```

---

## 🚀 Building from Source

### Prerequisites
- Windows 10 (1809+) or Windows 11 (recommended)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 / VS Code or command-line CLI

### Build Debug
```bash
dotnet build -c Debug
```

### Publish Native Self-Contained Single-File Binary
```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./publish
```

This generates a standalone `Aetherial.exe` bundled with all native WPF rendering components and dependencies.

---

## 📖 Documentation
- **Interactive Manual**: Open [`DiskOptimizer_Manual.html`](DiskOptimizer_Manual.html) in your browser.
- **Expert PDF Guide**: View [`DiskOptimizer_Podrecznik_Ekspercki.pdf`](DiskOptimizer_Podrecznik_Ekspercki.pdf).

---

## ⚖️ License
Distributed under the MIT License. See `LICENSE` for more information.
