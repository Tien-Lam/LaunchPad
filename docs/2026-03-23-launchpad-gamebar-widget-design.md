# LaunchPad - Xbox Game Bar Widget Design Spec

## Overview

LaunchPad is an Xbox Game Bar widget that provides a grid-based app launcher overlay. Users configure their favorite apps, URLs, and Store apps in a JSON file, then launch them directly from the Game Bar without leaving their game.

## Problem

Switching away from a fullscreen game to launch a companion app (Discord, OBS, a browser tab) is disruptive. Game Bar already overlays on top of games, making it a natural place for a quick launcher.

## Solution

A UWP XAML widget rendered inside Game Bar, backed by a Win32 companion process that handles launching EXEs outside the UWP sandbox and extracting application icons.

## Architecture

### Approach: Single Package with App Service IPC

One MSIX package containing:
1. **UWP Widget** - XAML grid UI, config reader, icon display
2. **Win32 Companion** - Desktop extension for EXE launching and icon extraction

Communication between widget and companion uses the UWP **App Service** pattern (`ValueSet` messages).

### Solution Structure

```
LaunchPad/
├── LaunchPad.sln
├── LaunchPad.Widget/              # UWP project (C#)
│   ├── App.xaml / App.xaml.cs     # Protocol activation handler
│   ├── LaunchPadWidget.xaml/.cs   # Main grid widget view
│   ├── Models/
│   │   └── LaunchItem.cs          # Data model for a launchable item
│   ├── Services/
│   │   └── CompanionClient.cs     # App Service client wrapper
│   ├── GameBar/                   # Icon assets for Game Bar widget list
│   ├── Assets/                    # App assets
│   ├── config.sample.json         # Example config
│   └── Package.appxmanifest       # Widget + desktop extension declarations
│
├── LaunchPad.Companion/           # Win32 Console App (.NET)
│   ├── Program.cs                 # Entry point + App Service host
│   ├── LaunchHandler.cs           # Process.Start for EXEs, URL/protocol launches
│   └── IconExtractor.cs           # Extract icons from EXE, cache as PNG
│
└── LaunchPad.Shared/              # Shared class library
    └── LaunchItemConfig.cs        # Config deserialization types (shared between projects)
```

### Technology Stack

- **Language**: C#
- **Widget UI**: UWP XAML
- **Companion**: .NET Win32 Console App
- **SDK**: Microsoft.Gaming.XboxGameBar NuGet package
- **IDE**: Visual Studio 2022 with UWP workload + Windows SDK 19041+
- **Target**: Windows 10 19041+

## Configuration

### Location

`%LOCALAPPDATA%\LaunchPad\config.json`

Both the widget and companion read from this path.

### Format

```json
{
  "items": [
    {
      "name": "Discord",
      "type": "exe",
      "path": "C:\\Users\\lamti\\AppData\\Local\\Discord\\Update.exe",
      "args": "--processStart Discord.exe",
      "icon": null
    },
    {
      "name": "YouTube",
      "type": "url",
      "path": "https://youtube.com",
      "icon": null
    },
    {
      "name": "Spotify",
      "type": "store",
      "path": "spotify:",
      "icon": "C:\\path\\to\\custom-icon.png"
    }
  ]
}
```

### Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | yes | Display label shown under the icon |
| `type` | `"exe"` \| `"url"` \| `"store"` | yes | Determines launch behavior |
| `path` | string | yes | EXE file path, URL, or protocol URI |
| `args` | string | no | Command-line arguments (EXE only) |
| `icon` | string \| null | no | Custom icon path; null = auto-extract |

Items appear in the grid in array order.

### Missing or Invalid Config

- **Config file missing**: Widget shows a helpful empty state with the expected config path (`%LOCALAPPDATA%\LaunchPad\config.json`) and a note to create it.
- **Malformed JSON**: Widget shows an error message with the parse error details.
- **Empty items array**: Widget shows the empty state (same as missing config).

## Widget UI

### Layout

4-column grid of square tiles. Each tile displays an icon (48x48) and a short text label below it. The widget targets ~12 items visible (4x3) without scrolling.

### Widget Size (manifest)

- **Initial**: 400w x 350h
- **Min**: 300w x 250h
- **Max**: 600w x 500h
- **Resize**: Horizontal and vertical enabled

### Behaviors

- **Hover**: Subtle highlight/glow on the tile
- **Click**: Sends launch command to companion; brief pulse/flash feedback
- **Overflow**: Vertical scrolling (additional rows) for > 12 items
- **Empty slots**: Not rendered; grid ends at the last configured item
- **Theming**: Uses `RequestedTheme` to match Game Bar's light/dark mode

### Manifest Properties

- `Type`: Standard
- `HomeMenuVisible`: true
- `PinningSupported`: true
- `ActivateAfterInstall`: true
- `FavoriteAfterInstall`: true
- `AllowForegroundTransparency`: true

## App Service Communication

### Service Name

`com.launchpad.service`

### Lifecycle

1. Widget activates via Game Bar protocol activation
2. Widget starts companion via `FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync()`
3. Widget opens `AppServiceConnection` to the companion
4. Companion receives the connection and begins handling requests
5. Widget sends commands, companion responds
6. When widget closes, App Service connection drops, companion exits

### Protocol

**Launch Command**

Request:
```
action = "launch"
type   = "exe" | "url" | "store"
path   = "C:\\...\\app.exe" | "https://..." | "spotify:"
args   = "--flag"  (optional)
```

Response:
```
status = "ok" | "error"
error  = "..."  (if status == "error")
```

**Extract Icon Command** (for EXE items)

Request:
```
action = "extract-icon"
path   = "C:\\...\\app.exe"
```

Response:
```
status   = "ok" | "error"
iconPath = "C:\\...\\LaunchPad\\icons\\abc123.png"
```

**Fetch Favicon Command** (for URL items)

Request:
```
action = "fetch-favicon"
url    = "https://youtube.com"
```

Response:
```
status   = "ok" | "error"
iconPath = "C:\\...\\LaunchPad\\icons\\abc123.png"
```

## Icon Strategy

### Cache Location

`%LOCALAPPDATA%\LaunchPad\icons\`

### Extraction per Type

| Type | Strategy | Fallback |
|------|----------|----------|
| `exe` | Companion calls `Icon.ExtractAssociatedIcon(path)`, saves as PNG. Filename: SHA256(path) + `.png` | Generic app icon |
| `url` | Companion fetches `https://www.google.com/s2/favicons?domain={host}&sz=64`, caches locally (avoids UWP sandbox network restrictions) | Generic globe icon |
| `store` | Generic app icon (protocol URIs don't have extractable icons) | Generic app icon |
| custom | User-provided path used directly, no extraction | N/A |

### Cache Invalidation

Compare file modification time of source EXE against cached icon. Re-extract if source is newer.

## Data Flow

```
1. User opens Game Bar (Win+G)
2. Game Bar activates LaunchPad via Protocol (ms-gamebarwidget:)
3. App.OnActivated → creates XboxGameBarWidget → navigates to LaunchPadWidget.xaml
4. Widget starts companion via FullTrustProcessLauncher
5. Widget reads config.json from %LOCALAPPDATA%\LaunchPad\
6. For EXE items without cached icons → sends "extract-icon" to companion
7. Widget renders 4-column grid with icons + labels
8. User clicks tile → Widget sends "launch" to companion
9. Companion launches EXE (Process.Start) / URL (shell execute) / protocol
10. Widget shows brief visual feedback (pulse animation)
```

## Scope & Phasing

### v1 (this spec)
- Grid widget with 4-column layout
- JSON config file
- Launch EXEs, URLs, and Store/protocol apps
- Icon auto-extraction for EXEs
- Favicon fetching for URLs
- Custom icon override

### Future (out of scope)
- Settings widget UI for editing config
- Drag-and-drop reordering
- Categories/groups
- Search/filter
- Recently launched / frecency sorting
- Hotkey support via XboxGameBarHotkeyWatcher

## Verification

1. Build and deploy the UWP package to local machine
2. Open Game Bar (Win+G), verify LaunchPad appears in widget list
3. Create a config.json with test items (EXE, URL, store app)
4. Open widget, verify grid renders with correct icons and labels
5. Click each tile type, verify the target app launches
6. Test with > 12 items, verify scrolling works
7. Test with empty config, verify graceful empty state
8. Test dark/light mode theming
