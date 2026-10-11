<div align="center">

<img src="WinWeaver/Resources/logo.png" width="96" alt="WinWeaver">

# WinWeaver

**Windows 11 tuning in a couple of clicks — privacy, appearance, speed and maintenance.**
No PowerShell windows or batch files. Every change is recorded in History, and most can be undone.

[![Download](https://img.shields.io/github/v/release/fel7xxx/WinWeaver?label=Download&color=84ccf2)](https://github.com/fel7xxx/WinWeaver/releases/latest)
[![Verify](https://github.com/fel7xxx/WinWeaver/actions/workflows/verify.yml/badge.svg)](https://github.com/fel7xxx/WinWeaver/actions/workflows/verify.yml)
[![License: GPL-3.0](https://img.shields.io/badge/license-GPL--3.0-blue)](LICENSE)

[Русский](README.md) · **English**

<img src="screenshots/en/01_home.png" width="860" alt="WinWeaver home page">

</div>

## What's new in 1.0.2

- **New Avalonia-based interface** (Skia) instead of GDI+: smoother animations and scrolling, crisper text on scaled displays.
- **New navigation:** a top menu "Home · System · Tools · Settings" and section tabs with counters.
- **Recommended** — everything WinWeaver suggests for this PC on one page, with an index and a per-section breakdown.
- **Details panel** for every setting: why it's worth it, who it may not suit, risk, whether a restart is needed and how to roll it back.
- **Search and commands** with `Ctrl+K` — jump to any setting or action.
- More reliable rollback: History detects case-only and value-type changes in the registry, and failed rollbacks are no longer reported as successful.

Full list — in [CHANGELOG.md](CHANGELOG.md).

## Download

1. Open the [latest release](https://github.com/fel7xxx/WinWeaver/releases/latest) and download the **WinWeaver** archive.
2. Extract it to a separate folder and run **WinWeaver.exe**. Windows will ask for administrator rights — they're needed to change system settings.
3. On first launch pick the language and how to run it: **install** to Program Files (Start menu shortcut, removal via "Installed apps") or **run without installing**.

> **Starting with 1.0.2 the program is a folder with WinWeaver.exe and libraries** (~30 MB), not a single exe. Run it only from the extracted folder.
>
> **Updating from 1.0.x:** close WinWeaver and run the new version from the extracted folder — it will offer to update the installed copy. Settings and History (`C:\ProgramData\WinWeaver`) are kept.

> **SmartScreen.** The program isn't signed with a paid certificate, so Windows may show "Windows protected your PC". Click **More info → Run anyway**. You can verify the download with the SHA-256 from the release.

**Requirements:** Windows 11 (22H2 – 26H2), administrator rights. .NET Framework 4.8 is already part of Windows 11.

## Features

| | |
|---|---|
| **Auto-tune** | Three levels — Basic, Optimal, Maximum. Every step is shown before it runs and can be turned off. A restore point is created first. |
| **Recommended** | A summary across all sections: what else can be improved on this PC, with an index and a hint for every setting. |
| **Privacy** | Telemetry, Copilot and Recall, ads and recommendations, Bing in search, location, Defender, BitLocker, Windows Update pause and mode. |
| **Appearance** | Dark theme, classic context menu, taskbar, Start, File Explorer. |
| **Performance** | Power plan, system responsiveness, gaming settings, HAGS, timer, priorities. |
| **Programs** | Uninstall programs together with leftovers, built-in Windows apps, startup. |
| **Network** | Ping, DNS choice (including encrypted DNS), network settings and tools. |
| **Fixes** | Common Windows problems fixed with one button: internet, Start, icons, sound, printer, Windows Update… |
| **Maintenance** | Disk cleanup, Storage Sense, system file check. |
| **Recovery** | Restore points: create, roll back, delete old ones. |
| **History** | Everything the program did. Changes can be undone on their own or as a group. |

### WeavOS style — optional
On the second auto-tune step you can set the **WeavOS** wallpaper (4 variants in 4K), show "WeavOS" as the system name in "About", and set the computer name and sign-in password. Every item can be skipped.

<img src="screenshots/en/11_autotune.png" width="720" alt="Auto-tune">

### Safety first
- **Details panel.** Every setting shows why it exists, who it may not suit, how risky it is and how to roll it back.
- **Expert mode.** Settings that weaken protection or may break programs (turning Defender off completely, VBS/HVCI, Spectre mitigations, "Never notify" in UAC, disabling services, BitLocker decryption) stay hidden until you enable the mode in Settings.
- **Edge** is removed only after your chosen browser is installed, and only with its official uninstaller; WebView2, which other programs need, stays.
- **"What Windows brought back".** Major updates sometimes turn settings back on — the program notices and offers to re-apply them.
- Downloaded installers are verified by digital signature or SHA-256. More in [SECURITY.md](SECURITY.md).

### More
- **Search and commands:** `Ctrl+K` — find any setting, section or action.
- **Profiles:** export and import settings as `.json` to move them to another PC.
- **New versions:** once a day the program can check GitHub and tell you about a release. It never downloads anything by itself.
- **Languages:** Russian and English (default follows Windows). You can add your own translation — see [CONTRIBUTING.md](CONTRIBUTING.md).
- **Full uninstall:** Settings → Remove WinWeaver — optionally undo all changes first.

## Screenshots

| | |
|---|---|
| <img src="screenshots/en/14_recommended.png" alt="Recommended"> | <img src="screenshots/en/02_privacy.png" alt="Privacy"> |
| <img src="screenshots/en/04_performance.png" alt="Performance"> | <img src="screenshots/en/05_programs.png" alt="Programs"> |
| <img src="screenshots/en/06_network.png" alt="Network"> | <img src="screenshots/en/07_maintenance.png" alt="Maintenance"> |
| <img src="screenshots/en/12_fixes.png" alt="Fixes"> | <img src="screenshots/en/09_history.png" alt="History"> |

## Command line

```
WinWeaver.exe /apply <light|optimal|gamer> [/browser <keep|firefox|chrome|brave|yandex|operagx>]
              [/opts "7zip,vc,ext,temp,drv"] [/wallpaper 1-4] [/nowall] [/noname]
              [/defender <asis|quiet>] [/restart]
WinWeaver.exe /uninstall [/quiet] [/undo]
WinWeaver.exe /version
WinWeaver.exe /help
```
`light` = Basic, `optimal` = Optimal, `gamer` = Maximum. Everything is recorded in History; the log is `C:\ProgramData\WinWeaver\tweaker.log`.

## FAQ

**Can I revert everything?** Almost. Every change is in History with an "Undo" button; things that can't be undone (for example, removed programs) are marked there. A restore point is created before auto-tune.

**My antivirus complains.** Programs that change system settings are sometimes flagged by mistake. The source is open — compare the SHA-256 from the release.

**Found a bug?** Settings → Report a bug — a GitHub form opens with the version and log filled in. Questions go to [Discussions](https://github.com/fel7xxx/WinWeaver/discussions).

## Support the project

WinWeaver is free and will stay free. If it helped you, you can leave a tip:

- **Boosty:** https://boosty.to/fel7xxx/donate
- **TON:** `UQB_35tg7SFtMrJVeU5W6MoB2shypmmpBcJUJKG5WfvCuOMc`
- **Bitcoin (BTC):** `bc1qpn0wasg3mkgdhepkpy5qjg9rmw793f85jq8f5m`

A ⭐ on GitHub and bug reports help a lot too.

## Building from source

```
# Windows build
dotnet publish WinWeaver/WinWeaver.csproj -f net48 -c Release -r win-x64 --self-contained false -o publish

# Demo without touching the system (Windows/Linux/macOS)
dotnet run --project WinWeaver/WinWeaver.csproj -f net8.0 -- /fake
```
Requires .NET SDK 8. Pre-release checks — `powershell -NoProfile -File scripts/verify.ps1` and the [checklist](docs/RELEASE-CHECKLIST.md). How the Avalonia UI works — [docs/AVALONIA-PORT.md](docs/AVALONIA-PORT.md) (Russian); rules — [CONTRIBUTING.md](CONTRIBUTING.md).

## License

© 2026 fel7xxx. The code is licensed under the [GNU GPL v3.0](LICENSE).
The **WinWeaver** and **WeavOS** names, logo and wallpapers — see [TRADEMARKS.md](TRADEMARKS.md): forks must be renamed and credited as "Based on WinWeaver by fel7xxx".

The program changes Windows system settings. It is provided "as is", without warranty — use restore points and History.
