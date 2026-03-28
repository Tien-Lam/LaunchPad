# LaunchPad

An Xbox Game Bar widget that launches apps and URLs from a configurable tile grid overlay.

<!-- TODO: Add screenshot here -->

## Features

- Launch EXEs and URLs from a dark-themed tile grid
- Automatic icon extraction for EXEs and favicon fetching for URLs
- Built-in config editor (WPF) for adding, removing, editing, and reordering items
- Runs as a Game Bar widget — open with Win+G while gaming

## Architecture

LaunchPad uses a two-process design to work around UWP sandbox restrictions:

- **Widget** (UWP) — the tile grid UI that runs inside Game Bar
- **Companion** (.NET 10 Win32) — handles file I/O, process launching, icon extraction, and hosts the config editor

The two processes communicate over Windows App Service IPC, packaged together in a single MSIX via a Windows Application Packaging Project.

```
LaunchPad.Widget/       # UWP XAML widget
LaunchPad.Companion/    # .NET 10 companion (WPF editor, IPC handlers)
LaunchPad.Shared/       # Shared library (config models, loader)
LaunchPad.Tests/        # xUnit tests
LaunchPad.Package/      # MSIX packaging and manifest
```

## Requirements

- Windows 10 19041+ with Xbox Game Bar
- Visual Studio 2022 with UWP workload and Windows SDK 19041+
- .NET 10 SDK

## Build

```bash
# Non-UWP projects
dotnet build LaunchPad.Shared/LaunchPad.Shared.csproj
dotnet build LaunchPad.Companion/LaunchPad.Companion.csproj
dotnet test LaunchPad.Tests/

# Full solution (requires VS / MSBuild)
msbuild LaunchPad.sln /p:Configuration=Debug /p:Platform=x64 /restore
```

## Deploy

```powershell
.\deploy.ps1
```

Builds the full solution with MSBuild and registers the package via loose-file deployment (no signing needed). Requires Visual Studio with the UWP workload installed. After deploying, open Game Bar (Win+G) and enable the LaunchPad widget from the widget menu.

## Uninstall

```powershell
# Remove the registered package
Get-AppxPackage *LaunchPad* | Remove-AppxPackage

# Remove config and cached icons
Remove-Item "$env:LOCALAPPDATA\LaunchPad" -Recurse -Force
```

## Configuration

Items are stored in `%LOCALAPPDATA%\LaunchPad\config.json`. Use the built-in editor (gear button in the widget) to manage items, or edit the JSON directly:

```json
{
  "items": [
    { "name": "Notepad", "type": "exe", "path": "C:\\Windows\\notepad.exe" },
    { "name": "YouTube", "type": "url", "path": "https://youtube.com" },
    { "name": "Spotify", "type": "store", "path": "spotify:" }
  ]
}
```

See [`config.sample.json`](config.sample.json) for a full example.

## Docs

- [Architecture](docs/ARCHITECTURE.md) — system overview, two-process design, project map
- [IPC Protocol](docs/IPC.md) — App Service actions, request/response fields, sequence flows
- [Config](docs/CONFIG.md) — JSON schema, item types, icon resolution
- [UI](docs/UI.md) — dark theme palette, XAML structure, interactive states
- [Deployment](docs/DEPLOYMENT.md) — build pipeline, VS deploy, manifest, troubleshooting
- [Testing](docs/TESTING.md) — test coverage, boundaries, manual test checklist
