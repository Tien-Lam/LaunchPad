# Architecture

## System Overview

LaunchPad is an Xbox Game Bar widget (Win+G) that launches apps from a configurable grid. It runs as a single MSIX package containing two processes that communicate over App Service IPC.

```
+---------------------------+          App Service IPC          +---------------------------+
|   LaunchPad.Widget (UWP)  | <-------- ValueSet msgs -------> | LaunchPad.Companion (.NET) |
|                           |        com.launchpad.service      |                           |
|  - XAML grid UI           |                                   |  - EXE/URL/Store launch   |
|  - Game Bar integration   |                                   |  - Config file I/O        |
|  - Icon display           |                                   |  - Icon extraction        |
|  - User interaction       |                                   |  - Favicon fetching       |
+---------------------------+                                   |  - File picker dialogs    |
              |                                                 +---------------------------+
              |  references                                                  |  references
              v                                                              v
+---------------------------+                              +---------------------------+
|  LaunchPad.Shared         |                              |  LaunchPad.Shared         |
|  (.NET Standard 2.0)      |                              |  (.NET Standard 2.0)      |
|  - ConfigModels           |                              |  - ConfigModels           |
|  - ConfigLoader           |                              |  - ConfigLoader           |
+---------------------------+                              +---------------------------+
```

## Why Two Processes?

UWP apps run in a sandbox. They cannot:
- Launch arbitrary EXEs (`Process.Start`)
- Read files outside their package folder
- Show Win32 file picker dialogs
- Extract icons from EXE files

The companion is a full-trust .NET 8 Win32 process that does all of this on behalf of the widget via App Service messages.

## Projects

| Project | Framework | Purpose |
|---------|-----------|---------|
| `LaunchPad.Widget` | UWP (netcore 6.2.14) | Game Bar widget UI |
| `LaunchPad.Companion` | .NET 8 (WinExe) | Full-trust helper process |
| `LaunchPad.Shared` | .NET Standard 2.0 | Config models, shared by both |
| `LaunchPad.Package` | WAPPROJ | MSIX packaging, manifest, assets |
| `LaunchPad.Tests` | .NET 8 (xunit) | Unit tests for companion/shared |

## IPC Protocol

All communication uses `ValueSet` messages over `AppServiceConnection`. Every request has an `action` field, every response has a `status` field.

### Actions

| Action | Direction | Purpose | Key Fields |
|--------|-----------|---------|------------|
| `launch` | Widget -> Companion | Launch an app | `type`, `path`, `args` |
| `load-config` | Widget -> Companion | Read config.json | `configPath` (optional) |
| `extract-icon` | Widget -> Companion | Get icon from EXE | `path` |
| `fetch-favicon` | Widget -> Companion | Get favicon for URL | `url` |
| `open-editor` | Widget -> Companion | Open WPF config editor | `configPath` |

### Response Statuses

- `ok` / `success` -- action completed
- `error` -- failed, `error` field has message
- `filenotfound` -- config file missing (load-config)

## Key Constraints

### UWP Sandbox
- Widget cannot access real filesystem -- all file I/O goes through companion
- `Environment.SpecialFolder.LocalApplicationData` returns virtualized path in UWP (`Packages\<id>\LocalState`). `GetDefaultConfigPath()` detects and strips this.
- Widget is only activated via `ms-gamebarwidget://` protocol. Direct launch (`OnLaunched`) calls `Exit()`.

### Companion Lifecycle
- Launched by widget via `FullTrustProcessLauncher` on load
- Named mutex (`Local\LaunchPadCompanion`) prevents duplicate instances
- Exits when App Service connection closes (Game Bar dismisses widget)
- `OutputType=WinExe` -- no console window

### Game Bar Integration
- Widget registered as `microsoft.gameBarUIExtension` in Package.appxmanifest
- Hardcoded dark theme (`#202020` background, `RequestedTheme="Dark"`) -- subscribes to Game Bar opacity events but not theme events
- Deploy via VS (F5) -- `Add-AppxPackage` registration doesn't reliably register with Game Bar

## Config

Location: `%LOCALAPPDATA%\LaunchPad\config.json`

Three item types: `exe` (local executables), `url` (web URLs), `store` (protocol URIs like `spotify:`). See `config.sample.json` for format.

## See Also

- [IPC Protocol](IPC.md) -- full message format and action reference for widget-companion communication
- [Configuration](CONFIG.md) -- config file schema, item types, icon resolution, and load/save details
- [UI](UI.md) -- widget XAML layout, theming, tile interactions, and Game Bar activation
- [Deployment](DEPLOYMENT.md) -- build pipeline, MSIX packaging, manifest structure, and troubleshooting
- [Testing](TESTING.md) -- test coverage, test patterns, and manual testing procedure
