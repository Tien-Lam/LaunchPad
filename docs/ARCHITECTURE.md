# Architecture

## System Overview

LaunchDeck is an Xbox Game Bar widget (Win+G) that launches apps from a configurable grid. It runs as a single MSIX package containing two processes that communicate over App Service IPC.

```
+---------------------------+          App Service IPC          +----------------------------------+
|   LaunchDeck.Widget (UWP)  | <-------- ValueSet msgs -------> | LaunchDeck.Companion (.NET 10)    |
|                           |        com.launchdeck.service      |                                  |
|  - XAML grid UI           |                                   |  - EXE/URL/Store launch          |
|  - Game Bar integration   |                                   |  - Config file I/O               |
|  - Icon display           |                                   |  - Icon extraction               |
|  - User interaction       |                                   |  - Favicon fetching              |
|                           |                                   |  - File picker dialogs           |
|  Services/                |                                   |  - Store app enumeration         |
|    CompanionClient.cs     |                                   |  - Window focus (NativeMethods)  |
|  Models/                  |                                   |                                  |
|    LaunchItem.cs          |                                   |  Editor/ (WPF)                   |
+---------------------------+                                   |    EditorWindow, EditorViewModel  |
              |                                                 |    EditorModel, EditorManager     |
              |  references                                     |    StoreAppPickerWindow           |
              v                                                 |    MessageDialog, ItemViewModel   |
+---------------------------+                                   |    RelayCommand, EditorTheme.xaml |
|  LaunchDeck.Shared         |                                   +----------------------------------+
|  (.NET Standard 2.0)      |                                                    |  references
|                           |                                                    v
|  LaunchDeckConfig         |                              +---------------------------+
|  LaunchItemConfig         |                              |  LaunchDeck.Shared         |
|  LaunchItemType (enum)    |                              |  (.NET Standard 2.0)      |
|  ConfigLoadResult         |                              |                           |
|  ConfigLoadStatus (enum)  |                              |  (same classes as left)   |
|  ConfigLoader (static)    |                              |                           |
|    Load, Save, ParseJson  |                              |                           |
+---------------------------+                              +---------------------------+
```

## Why Two Processes?

UWP apps run in a sandbox. They cannot:
- Launch arbitrary EXEs (`Process.Start`)
- Read files outside their package folder
- Show Win32 file picker dialogs
- Extract icons from EXE files

The companion is a full-trust .NET 10 Win32 process that does all of this on behalf of the widget via App Service messages.

## Projects

| Project | Framework | Purpose |
|---------|-----------|---------|
| `LaunchDeck.Widget` | UWP (`Microsoft.NETCore.UniversalWindowsPlatform` 6.2.14) | Game Bar widget UI |
| `LaunchDeck.Companion` | .NET 10 (WinExe) | Full-trust helper process |
| `LaunchDeck.Shared` | .NET Standard 2.0 | Config models, shared by both |
| `LaunchDeck.Package` | WAPPROJ | MSIX packaging, manifest, assets |
| `LaunchDeck.Tests` | .NET 10 (xunit) | Unit tests for companion/shared |

## IPC Protocol

All communication uses `ValueSet` messages over `AppServiceConnection`. Every request has an `action` field, every response has a `status` field.

### Actions

| Action | Direction | Purpose | Key Fields |
|--------|-----------|---------|------------|
| `launch` | Widget -> Companion | Launch an app | `type`, `path`, `args` |
| `load-config` | Widget -> Companion | Read config.json | `configPath` (optional) |
| `extract-icon` | Widget -> Companion | Get icon from EXE | `path` |
| `fetch-favicon` | Widget -> Companion | Get favicon for URL | `url` |
| `load-custom-icon` | Widget -> Companion | Load a user-specified icon file | `path` |
| `extract-store-icon` | Widget -> Companion | Get icon for a Store/UWP app | `aumid` |
| `open-editor` | Widget -> Companion | Open WPF config editor | `configPath` |
| `log` | Widget -> Companion | Write a log message to `companion.log` | `message` |
| `config-updated` | Companion -> Widget | Notify widget that config was saved in editor | (none) |

### Response Statuses

- `ok` / `success` -- action completed
- `error` -- failed, `error` field has message
- `filenotfound` -- config file missing (load-config)
- `parseerror` -- config JSON could not be deserialized (load-config)

## Key Constraints

### UWP Sandbox
- Widget cannot access real filesystem -- all file I/O goes through companion
- `Environment.SpecialFolder.LocalApplicationData` returns virtualized path in UWP (`Packages\<id>\LocalState`). `GetDefaultConfigPath()` detects and strips this.
- Widget is only activated via `ms-gamebarwidget://` protocol. Direct launch (`OnLaunched`) calls `Exit()`.

### Companion Lifecycle
- Launched by widget via `FullTrustProcessLauncher` on load
- Named mutex (`Local\LaunchDeckCompanion`) prevents duplicate instances; uses `mutex.WaitOne(500)` (500ms timeout) rather than immediate exit
- Logs diagnostics to `%LOCALAPPDATA%\LaunchDeck\companion.log` via `Log.cs`
- Registers a `ServiceClosed` handler that signals an exit event, so the companion exits when the App Service connection closes (Game Bar dismisses widget)
- On the widget side, the `ServiceClosed` handler sets `CompanionConnection = null` and calls `TryRelaunchCompanion()` with exponential backoff (1s, 2s, 4s) to re-establish the connection
- `OutputType=WinExe` -- no console window

### Game Bar Integration
- Widget registered as `microsoft.gameBarUIExtension` in `LaunchDeck.Package/Package.appxmanifest` (not the UWP project's own manifest)
- Hardcoded dark theme (`#202020` background, `RequestedTheme="Dark"`) -- subscribes to Game Bar `RequestedOpacityChanged` and `VisibleChanged` events (opacity adjusts background alpha; visibility triggers config reload) but not theme events
- Deploy via `deploy.ps1` (`Add-AppxPackage -Register`) or VS (F5)

## Config

Location: `%LOCALAPPDATA%\LaunchDeck\config.json`

Three item types: `exe` (local executables), `url` (web URLs), `store` (UWP/Store apps via `shell:AppsFolder\{AUMID}`). See `config.sample.json` for format.

## See Also

- [IPC Protocol](IPC.md) -- full message format and action reference for widget-companion communication
- [Configuration](CONFIG.md) -- config file schema, item types, icon resolution, and load/save details
- [UI](UI.md) -- widget XAML layout, theming, tile interactions, and Game Bar activation
- [Deployment](DEPLOYMENT.md) -- build pipeline, MSIX packaging, manifest structure, and troubleshooting
- [Testing](TESTING.md) -- test coverage, test patterns, and manual testing procedure
