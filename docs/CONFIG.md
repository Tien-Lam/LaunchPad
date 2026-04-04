# LaunchDeck Configuration Reference

## Config File Location

The config file lives at:

```
%LOCALAPPDATA%\LaunchDeck\config.json
```

Typical resolved path:

```
C:\Users\<username>\AppData\Local\LaunchDeck\config.json
```

### UWP Virtualized Path Problem

UWP apps (including Game Bar widgets) do not see the real `%LOCALAPPDATA%`. The system virtualizes it to:

```
C:\Users\<username>\AppData\Local\Packages\<PackageFamilyName>\LocalState
```

If the widget called `Environment.GetFolderPath(SpecialFolder.LocalApplicationData)` directly, it would get the virtualized path, and the companion process would resolve a different location. Both would think they are using "local app data" but would point to different directories.

`ConfigLoader.GetDefaultConfigPath()` fixes this by detecting the `\Packages\` segment in the path and stripping everything from that point onward, so both the widget and companion resolve to the same real `%LOCALAPPDATA%\LaunchDeck\config.json`.

Relevant code in `LaunchDeck.Shared/ConfigModels.cs`:

```csharp
var packagesIdx = localAppData.IndexOf(@"\Packages\", StringComparison.OrdinalIgnoreCase);
if (packagesIdx >= 0)
    localAppData = localAppData.Substring(0, packagesIdx);

return Path.Combine(localAppData, "LaunchDeck", "config.json");
```

If the directory does not exist when saving, `ConfigLoader.Save` creates it automatically.

---

## JSON Schema

The config file is a single JSON object with one top-level key, `items`, containing an array of launch item objects.

```json
{
  "items": [
    {
      "name": "...",
      "type": "...",
      "path": "...",
      "args": null,
      "icon": null
    }
  ]
}
```

### Top-Level Object

| Field   | Type    | Required | Description                   |
|---------|---------|----------|-------------------------------|
| `items` | array   | Yes      | Array of `LaunchItemConfig` objects. May be empty. |

### LaunchItemConfig

| Field  | Type     | Required | Default | Description |
|--------|----------|----------|---------|-------------|
| `name` | string   | Yes      | `""`    | Display name shown on the widget tile. |
| `type` | string   | Yes      | --      | One of `"Exe"`, `"Url"`, `"Store"`. Widget-side deserialization uses `JsonDocument` + `TryGetProperty` with two explicit casings per field (lowercase then PascalCase), and `Enum.TryParse(typeStr, true, ...)` for case-insensitive enum matching. Serialized as PascalCase via `JsonStringEnumConverter` (no `JsonNamingPolicy` specified). |
| `path` | string   | Yes      | `""`    | Target to launch. Meaning depends on `type` (see below). |
| `args` | string?  | No       | `null`  | Command-line arguments. Only meaningful for `type: "exe"`. |
| `icon` | string?  | No       | `null`  | Absolute path to a custom icon image file. When set, bypasses all automatic icon resolution. |

---

## Item Types

### `exe` -- Launch a Win32 Executable

The `path` field is the absolute filesystem path to an `.exe` file. The optional `args` field provides command-line arguments.

```json
{
  "name": "Notepad",
  "type": "exe",
  "path": "C:\\Windows\\notepad.exe",
  "args": null,
  "icon": null
}
```

With arguments:

```json
{
  "name": "Dev Server",
  "type": "exe",
  "path": "C:\\Program Files\\nodejs\\node.exe",
  "args": "server.js --port 3000",
  "icon": null
}
```

### `url` -- Open a URL in the Default Browser

The `path` field is a full URL (including scheme). Launched via the system's default browser.

```json
{
  "name": "YouTube",
  "type": "url",
  "path": "https://youtube.com",
  "args": null,
  "icon": null
}
```

### `store` -- Launch a Microsoft Store / UWP App

The `path` field is a `shell:AppsFolder\` URI containing the app's AUMID (Application User Model ID). The editor's "Add Store" button opens an edit dialog, and the Browse button within that dialog opens the Store App Picker which fills the AUMID and name automatically.

```json
{
  "name": "Spotify",
  "type": "store",
  "path": "shell:AppsFolder\\SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify",
  "args": null,
  "icon": null
}
```

```json
{
  "name": "Xbox",
  "type": "store",
  "path": "shell:AppsFolder\\Microsoft.GamingApp_8wekyb3d8bbwe!Microsoft.Xbox.App",
  "args": null,
  "icon": null
}
```

---

## Icon Resolution

Icons are resolved per-item when the widget loads. The resolution follows a chain of fallbacks, evaluated top to bottom. The first successful result wins.

### Resolution Chain

1. **Custom icon** (all types) -- If the item's `icon` field is non-null, the widget sends a `load-custom-icon` IPC request to the companion, which reads the file, converts it to PNG if needed (via `IconExtractor.LoadCustomIcon`), and returns the image data as base64 bytes.

2. **Extracted from EXE** (type `exe` only) -- The widget sends an `extract-icon` IPC request. The companion calls `Icon.ExtractAssociatedIcon(exePath)` to pull the embedded icon from the executable, converts it to PNG, saves it to the icon cache, reads the cached file, and returns the data as base64 bytes.

3. **Fetched favicon** (type `url` only) -- The widget sends a `fetch-favicon` IPC request. The companion fetches a favicon via the Google favicon service: `https://www.google.com/s2/favicons?domain={host}&sz=64`. The result is saved to the icon cache as a PNG, read back, and returned as base64 bytes.

4. **Extracted from Store app** (type `store` only) -- The widget sends an `extract-store-icon` IPC request with the AUMID. The companion calls `IconExtractor.ExtractStoreAppIcon`, which locates the app's installed package, reads its `AppxManifest.xml` to find the logo path, and returns the icon data as base64 bytes.

5. **Default asset** -- If all of the above fail, the widget falls back to a bundled asset:
   - `ms-appx:///Assets/DefaultGlobe.png` for `url` items
   - `ms-appx:///Assets/DefaultApp.png` for `exe` and `store` items

### Icon Cache

Cached icons are stored at:

```
%LOCALAPPDATA%\LaunchDeck\icons\
```

Note: Unlike the config path, the icon cache directory is resolved by the companion process (a desktop .NET 10 app, not UWP), so `Environment.GetFolderPath` returns the real `%LOCALAPPDATA%` without virtualization issues.

Each cached icon is named using a truncated SHA-256 hash of the input (the EXE path or URL):

```
<first 16 hex chars of SHA256>.png
```

For example, `C:\Windows\notepad.exe` produces a cache file like `a1b2c3d4e5f67890.png`.

**Cache invalidation:**

- **EXE icons**: The cache is invalidated when the EXE's last-write timestamp is newer than the cached PNG's last-write timestamp. This means updating or reinstalling an application causes re-extraction on next load.
- **Favicons**: Cached favicons expire after 7 days. On the next load after expiry, the favicon is re-fetched from Google's favicon service. If the re-fetch fails, the stale cache is not used — the item falls back to the default globe icon.

The cache directory is created automatically on first use by `IconExtractor.GetIconCacheDir()`.

---

## How Config Is Loaded

The widget (UWP) cannot read arbitrary filesystem paths due to the UWP sandbox. All config access goes through the companion process (Win32, .NET 10) via Windows App Service IPC.

### Load Sequence

1. **Widget starts** -- `LaunchDeckWidget.OnLoaded` fires and calls `EnsureCompanionAsync()`, which launches the companion via `FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync()` and then polls for the App Service connection with 3 retry attempts (100ms polling x 100 iterations per attempt).

2. **Widget requests config** -- `CompanionClient.LoadConfigAsync()` sends a `ValueSet` message:
   ```
   { "action": "load-config" }
   ```
   An optional `"configPath"` key can override the default path.

3. **Companion reads file** -- `HandleLoadConfig` in the companion resolves the config path (using `GetDefaultConfigPath()` if not overridden), calls `ConfigLoader.Load(path)`, and returns a `ValueSet` response:
   ```
   { "status": "success|filenotfound|parseerror", "configPath": "...", "json": "...", "error": "..." }
   ```
   - On success: `status` = `"success"`, `json` = serialized config JSON.
   - On file not found: `status` = `"filenotfound"`, no `json` key.
   - On parse error: `status` = `"parseerror"`, `error` = exception message.

4. **Widget processes response** -- `CompanionClient` deserializes the JSON back into a `LaunchDeckConfig` and returns a tuple of `(ConfigLoadStatus, LaunchDeckConfig?, string?, string?)`.

5. **Widget populates grid** -- `LoadConfigAsync` maps each `LaunchItemConfig` into a `LaunchItem` view model, adds it to the `ObservableCollection<LaunchItem>`, then kicks off `LoadIconsAsync` for icon resolution.

6. **Icon resolution** -- For each item, the widget sends IPC requests (`load-custom-icon`, `extract-icon`, `fetch-favicon`, or `extract-store-icon` depending on item type and config) to the companion, which performs the actual I/O and returns base64-encoded icon data. The widget decodes the data into a `BitmapImage`. See the Resolution Chain above for the full fallback order.

### Reload Triggers

The widget reloads the config automatically in response to:

- **`CompanionConnected` event** -- fires when the companion (re)connects via App Service, ensuring the grid is populated after startup or reconnection.
- **`VisibleChanged` event** -- fires when the widget is shown/hidden in Game Bar, so the grid reflects any changes made while the widget was hidden.
- **`config-updated` push** -- the companion sends this after the editor saves, triggering an immediate reload.

### Error States in the Widget

| Condition | Widget behavior |
|-----------|----------------|
| Config file not found | Shows empty state: "No apps configured — Click the gear button to add apps" |
| JSON parse error | Shows empty state: "Invalid config file" with the parse error message |
| Config exists but `items` is empty | Shows empty state: "No apps configured — Click the gear button to add apps" |
| Companion not connected | `CompanionClient` returns `FileNotFound` status with error "Companion not connected" |
| App Service communication failure | `CompanionClient` returns `FileNotFound` status with error "App Service error" |

---

## How Items Are Added (Config Editor)

Users add or edit items by opening the WPF config editor from the widget. Clicking the gear button triggers an IPC request that opens the editor window in the companion process.

### Flow

1. **User clicks gear button** -- `OnEditClick` in the widget disables the button and calls `CompanionClient.OpenEditorAsync()`.

2. **Widget sends IPC request**:
   ```
   { "action": "open-editor", "configPath": "<resolved path>" }
   ```

3. **Companion opens WPF editor** -- The companion launches `EditorWindow` on a persistent STA thread, loading the current config. If the editor is already open, the existing window is focused instead.

4. **User adds/edits items** -- The editor provides a UI for adding, removing, and editing launch items (EXE, URL, Store). Adding an item opens an inline edit dialog where the user fills in name, path, and optional fields. For Store items, the Browse button within the edit dialog opens the Store App Picker, which lists installed UWP/Store apps and fills in the AUMID and name automatically.

5. **User clicks "Save and Refresh"** -- The editor calls `ConfigLoader.Save(configPath, config)` to write the updated JSON back to disk with `WriteIndented = true`.

6. **Companion notifies widget** -- After saving, the companion sends an unsolicited push message to the widget:
   ```
   { "action": "config-updated" }
   ```

7. **Widget reloads** -- The widget's `ConfigUpdated` event handler calls `LoadConfigAsync()`, which reloads the full config from disk (via the companion) and re-resolves all icons.

---

## ConfigLoadResult and ConfigLoadStatus

`ConfigLoadResult` is the return type of `ConfigLoader.Load(path)`, used internally by the companion.

### ConfigLoadResult Fields

| Field          | Type               | Description |
|----------------|--------------------|-------------|
| `Config`       | `LaunchDeckConfig?` | The deserialized config object. Null on failure. |
| `Status`       | `ConfigLoadStatus` | Enum indicating the outcome. |
| `ErrorMessage` | `string?`          | Populated only on `ParseError`. Contains the `JsonException.Message`. |

### ConfigLoadStatus Enum Values

| Value          | Meaning |
|----------------|---------|
| `Success`      | File was found and deserialized without error. `Config` is populated. |
| `FileNotFound` | `File.Exists(path)` returned false. `Config` is null. `ErrorMessage` is null. |
| `ParseError`   | File exists but `JsonSerializer.Deserialize` threw a `JsonException`. `Config` is null. `ErrorMessage` contains the exception message. |

Note: `ConfigLoader.Load` uses `PropertyNameCaseInsensitive = true`, so field names in the JSON are matched case-insensitively (e.g., `"Name"`, `"name"`, and `"NAME"` are all accepted).

---

## Full Example Config

```json
{
  "items": [
    {
      "name": "Notepad",
      "type": "exe",
      "path": "C:\\Windows\\notepad.exe",
      "args": null,
      "icon": null
    },
    {
      "name": "Calculator",
      "type": "exe",
      "path": "C:\\Windows\\System32\\calc.exe",
      "args": null,
      "icon": null
    },
    {
      "name": "YouTube",
      "type": "url",
      "path": "https://youtube.com",
      "args": null,
      "icon": null
    },
    {
      "name": "GitHub",
      "type": "url",
      "path": "https://github.com",
      "args": null,
      "icon": null
    },
    {
      "name": "Spotify",
      "type": "store",
      "path": "shell:AppsFolder\\SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify",
      "args": null,
      "icon": null
    },
    {
      "name": "Xbox",
      "type": "store",
      "path": "shell:AppsFolder\\Microsoft.GamingApp_8wekyb3d8bbwe!Microsoft.Xbox.App",
      "args": null,
      "icon": null
    }
  ]
}
```

---

## Key Source Files

| File | Role |
|------|------|
| `LaunchDeck.Shared/ConfigModels.cs` | `LaunchDeckConfig`, `LaunchItemConfig`, `LaunchItemType`, `ConfigLoadResult`, `ConfigLoadStatus`, `ConfigLoader` |
| `LaunchDeck.Companion/Program.cs` | IPC dispatcher: `HandleLoadConfig`, `HandleOpenEditor` |
| `LaunchDeck.Companion/IconExtractor.cs` | EXE icon extraction, favicon fetching, store app icon extraction, custom icon loading, cache management |
| `LaunchDeck.Companion/StoreAppEnumerator.cs` | Store app enumeration, AUMID/package lookup, logo path resolution |
| `LaunchDeck.Companion/Editor/StoreAppPickerWindow.xaml.cs` | Store App Picker dialog for selecting installed UWP/Store apps |
| `LaunchDeck.Widget/Services/CompanionClient.cs` | Widget-side IPC client: `LoadConfigAsync`, `ExtractIconAsync`, `FetchFaviconAsync`, `LoadCustomIconAsync`, `ExtractStoreIconAsync`, `OpenEditorAsync` |
| `LaunchDeck.Widget/LaunchDeckWidget.xaml.cs` | Widget UI: `LoadConfigAsync`, `LoadIconsAsync`, `OnEditClick` |
| `config.sample.json` | Example config file with all three item types |

## See Also

- [Architecture](ARCHITECTURE.md) -- system overview and why config is loaded through the companion
- [IPC Protocol](IPC.md) -- `load-config` and `open-editor` message format for config operations over IPC
- [UI](UI.md) -- how config items drive the tile grid display and icon resolution
