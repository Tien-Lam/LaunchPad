# LaunchDeck

[![Microsoft Store](https://img.shields.io/badge/Microsoft_Store-Install-0078D4?style=flat&logo=microsoft)](https://apps.microsoft.com/detail/9PJHCMMVQ6HK)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

An Xbox Game Bar widget that launches apps, URLs, and Store apps from a configurable tile grid overlay.

[Xbox mode](https://support.microsoft.com/en-us/topic/windows-gaming-full-screen-experience-67fb8d12-5467-4a95-8adf-0a10789576ab) (formerly Xbox Full Screen Experience) turns Windows into a console-like fullscreen shell, but its library is game-focused — there's no easy way to launch non-game apps, utilities, or URLs without switching back to the desktop. LaunchDeck fills that gap as a Game Bar widget you can open with Win+G to launch anything without leaving the experience.

<!-- TODO: Add screenshot here -->

## Install

<a href="https://apps.microsoft.com/detail/9PJHCMMVQ6HK">
  <img src="https://get.microsoft.com/images/en-us%20dark.svg" alt="Get it from Microsoft" height="80"/>
</a>

Or build from source — see [Build](#build) and [Deploy](#deploy) below.

## Features

- Launch EXEs, URLs, and Microsoft Store apps from a dark-themed tile grid
- Automatic icon extraction for EXEs, favicons for URLs, and package icons for Store apps
- Built-in config editor with Store app picker — browse installed apps and add them with one click
- Runs as a Game Bar widget — open with Win+G while gaming

## Architecture

LaunchDeck uses a two-process design to work around UWP sandbox restrictions:

- **Widget** (UWP) — the tile grid UI that runs inside Game Bar
- **Companion** (.NET 10 Win32) — handles file I/O, process launching, icon extraction, and hosts the config editor

The two processes communicate over Windows App Service IPC, packaged together in a single MSIX via a Windows Application Packaging Project.

```
LaunchDeck.Widget/       # UWP XAML widget
LaunchDeck.Companion/    # .NET 10 companion (WPF editor, IPC handlers)
LaunchDeck.Shared/       # Shared library (config models, loader)
LaunchDeck.Tests/        # xUnit tests
LaunchDeck.Package/      # MSIX packaging and manifest
```

## Requirements

- Windows 10 19041+ with Xbox Game Bar
- Visual Studio 2022 with UWP workload and Windows SDK 10.0.26100.0
- .NET 10 SDK

## Build

```bash
# Non-UWP projects
dotnet build LaunchDeck.Shared/LaunchDeck.Shared.csproj
dotnet build LaunchDeck.Companion/LaunchDeck.Companion.csproj
dotnet test LaunchDeck.Tests/

# Full solution (requires VS / MSBuild)
msbuild LaunchDeck.sln /p:Configuration=Debug /p:Platform=x64 /restore
```

## Deploy

```powershell
.\deploy.ps1
```

Builds the full solution with MSBuild and registers the package via loose-file deployment (no signing needed). Requires Visual Studio with the UWP workload installed. After deploying, open Game Bar (Win+G) and enable the LaunchDeck widget from the widget menu.

## Uninstall

```powershell
.\Uninstall.ps1
```

Or manually:

```powershell
# Remove the registered package
Get-AppxPackage *LaunchDeck* | Remove-AppxPackage

# Remove config and cached icons
Remove-Item "$env:LOCALAPPDATA\LaunchDeck" -Recurse -Force
```

## Configuration

Items are stored in `%LOCALAPPDATA%\LaunchDeck\config.json`. Use the built-in editor (gear button in the widget) to manage items, or edit the JSON directly:

```json
{
  "items": [
    { "name": "Notepad", "type": "exe", "path": "C:\\Windows\\notepad.exe" },
    { "name": "YouTube", "type": "url", "path": "https://youtube.com" },
    { "name": "Spotify", "type": "store", "path": "shell:AppsFolder\\SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify" },
    { "name": "Dev Server", "type": "exe", "path": "C:\\tools\\server.exe", "args": "--port 8080" },
    { "name": "Discord", "type": "exe", "path": "C:\\Discord\\Discord.exe", "icon": "C:\\icons\\discord.png" }
  ]
}
```

Each item requires `name`, `type`, and `path`. Optional fields: `args` (command-line arguments for EXE items) and `icon` (custom icon image path, overrides auto-extraction). See [`config.sample.json`](config.sample.json) for a full example.

## Docs

- [Architecture](docs/ARCHITECTURE.md) — system overview, two-process design, project map
- [IPC Protocol](docs/IPC.md) — App Service actions, request/response fields, sequence flows
- [Config](docs/CONFIG.md) — JSON schema, item types, icon resolution
- [UI](docs/UI.md) — dark theme palette, XAML structure, interactive states
- [Deployment](docs/DEPLOYMENT.md) — build pipeline, VS deploy, manifest, troubleshooting
- [Testing](docs/TESTING.md) — test coverage, boundaries, manual test checklist
