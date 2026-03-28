# IPC Protocol Reference

Inter-process communication protocol between the LaunchPad UWP widget and the .NET 8 Win32 companion process.

## Transport

Communication uses **Windows App Service** (`Windows.ApplicationModel.AppService`). Messages are `ValueSet` dictionaries (string keys, object values). All values used in this protocol are strings.

- **Service name:** `com.launchpad.service`
- **Registration:** Declared in `Package.appxmanifest` under `<uap:Extension Category="windows.appService">`
- **Direction:** Primarily request/response (widget sends requests, companion responds via `SendResponseAsync`). The companion can also send unsolicited push messages to the widget (e.g., `config-updated`).

The App Service runs in-process with the widget (background task), giving the companion a direct pipe into the widget's process. Both processes live in the same MSIX package.

## Connection Lifecycle

### Companion (client) -- Program.cs

1. On startup, the companion acquires a single-instance mutex (`Local\LaunchPadCompanion`). If already held, the process exits immediately.
2. Creates an `AppServiceConnection` with:
   - `AppServiceName = "com.launchpad.service"`
   - `PackageFamilyName = Package.Current.Id.FamilyName`
3. Registers `RequestReceived` handler for inbound messages.
4. Registers `ServiceClosed` handler that signals an exit event.
5. Calls `OpenAsync()`. If the status is not `AppServiceConnectionStatus.Success`, exits.
6. Blocks on a `ManualResetEvent` until `ServiceClosed` fires.

### Widget (server) -- App.xaml.cs

1. The system calls `OnBackgroundActivated` when the companion opens the connection.
2. The widget checks `args.TaskInstance.TriggerDetails` for `AppServiceTriggerDetails`.
3. Stores the `AppServiceConnection` in the static `App.CompanionConnection` property, making it available to all widget code.
4. Registers a `Canceled` handler on the background task that clears `CompanionConnection` and completes the deferral.
5. On app suspension (`OnSuspending`), `CompanionConnection` is set to `null`.

### Connection Teardown

The connection ends when either:

- The companion process exits (widget receives task cancellation).
- The widget suspends or is closed (companion receives `ServiceClosed`).
- The system reclaims the background task.

There is no reconnection logic. If the connection drops, `CompanionConnection` becomes `null` and all subsequent `CompanionClient` calls fail gracefully (return default/false/null).

## Message Format

### Request (widget to companion)

Every request is a `ValueSet` with a required `action` key:

| Key      | Type   | Required | Description                    |
|----------|--------|----------|--------------------------------|
| `action` | string | yes      | Identifies the handler to invoke |

Additional keys depend on the action (see below).

### Response (companion to widget)

Every response includes a `status` key:

| Key      | Type   | Always present | Values                                       |
|----------|--------|----------------|----------------------------------------------|
| `status` | string | yes            | `"ok"`, `"error"`, `"cancelled"`, or a `ConfigLoadStatus` name |
| `error`  | string | no             | Human-readable error message (present when `status` is `"error"` or `"parseerror"`) |

### Error Handling

**Companion-side:** The `OnRequestReceived` handler wraps all dispatch logic in a try/catch. Any unhandled exception produces:

```
{ "status": "error", "error": "<exception message>" }
```

Unknown action values produce:

```
{ "status": "error", "error": "Unknown action: <value>" }
```

**Widget-side:** `CompanionClient` methods check two layers before returning results:

1. `App.CompanionConnection == null` -- companion not connected. Returns a typed failure (e.g., `false`, `null`, or a tuple with error info).
2. `response.Status != AppServiceResponseStatus.Success` -- transport-level failure. Returns a typed failure.
3. Response `status` field -- action-level success/failure.

No exceptions are thrown to callers. All methods return result tuples or nullable values.

## Actions

### `launch`

Launches an application (EXE, URL, or Store app) via `Process.Start` with `UseShellExecute = true`.

**Request:**

| Key      | Type   | Required | Description                                           |
|----------|--------|----------|-------------------------------------------------------|
| `action` | string | yes      | `"launch"`                                            |
| `type`   | string | yes      | Launch type: `"exe"`, `"url"`, or `"store"`           |
| `path`   | string | yes      | Executable path, URL, or `ms-windows-store://` URI    |
| `args`   | string | no       | Command-line arguments (only used for `"exe"` type)   |

**Response:**

| Key      | Type   | Condition        | Description           |
|----------|--------|------------------|-----------------------|
| `status` | string | always           | `"ok"` or `"error"`  |
| `error`  | string | on failure       | Exception message     |

**Example request:**

```
ValueSet {
    ["action"] = "launch",
    ["type"]   = "exe",
    ["path"]   = "C:\\Games\\game.exe",
    ["args"]   = "--fullscreen"
}
```

**Example success response:**

```
ValueSet { ["status"] = "ok" }
```

**Example error response:**

```
ValueSet { ["status"] = "error", ["error"] = "The system cannot find the file specified." }
```

**Widget client:** `CompanionClient.LaunchAsync(string type, string path, string? args)` returns `bool` (`true` on success).

---

### `extract-icon`

Extracts the associated icon from an EXE file and saves it as a PNG in the icon cache directory (`%LOCALAPPDATA%\LaunchPad\icons\`). Uses SHA-256 hash of the input path for the cache filename. Returns a cached result if the cache file is newer than the EXE.

**Request:**

| Key      | Type   | Required | Description              |
|----------|--------|----------|--------------------------|
| `action` | string | yes      | `"extract-icon"`         |
| `path`   | string | yes      | Full path to the EXE file |

**Response:**

| Key        | Type   | Condition  | Description                                   |
|------------|--------|------------|-----------------------------------------------|
| `status`   | string | always     | `"ok"` or `"error"`                           |
| `iconPath` | string | on success | Absolute path to the cached PNG icon file     |

**Example request:**

```
ValueSet {
    ["action"] = "extract-icon",
    ["path"]   = "C:\\Games\\game.exe"
}
```

**Example success response:**

```
ValueSet {
    ["status"]   = "ok",
    ["iconPath"] = "C:\\Users\\user\\AppData\\Local\\LaunchPad\\icons\\a1b2c3d4e5f67890.png"
}
```

**Widget client:** `CompanionClient.ExtractIconAsync(string exePath)` returns `string?` (icon path on success, `null` on failure).

---

### `fetch-favicon`

Downloads a website's favicon via Google's favicon service (`https://www.google.com/s2/favicons?domain=<host>&sz=64`) and caches it as a PNG. Uses SHA-256 hash of the URL for the cache filename. Returns the cached path immediately if the file already exists.

**Request:**

| Key      | Type   | Required | Description                    |
|----------|--------|----------|--------------------------------|
| `action` | string | yes      | `"fetch-favicon"`              |
| `url`    | string | yes      | The website URL to fetch favicon for |

**Response:**

| Key        | Type   | Condition  | Description                                |
|------------|--------|------------|--------------------------------------------|
| `status`   | string | always     | `"ok"` or `"error"`                        |
| `iconPath` | string | on success | Absolute path to the cached favicon file   |

**Example request:**

```
ValueSet {
    ["action"] = "fetch-favicon",
    ["url"]    = "https://store.steampowered.com"
}
```

**Example success response:**

```
ValueSet {
    ["status"]   = "ok",
    ["iconPath"] = "C:\\Users\\user\\AppData\\Local\\LaunchPad\\icons\\9f8e7d6c5b4a3210.png"
}
```

**Widget client:** `CompanionClient.FetchFaviconAsync(string url)` returns `string?` (icon path on success, `null` on failure).

---

### `load-config`

Loads the LaunchPad configuration from a JSON file on disk. The default path is `%LOCALAPPDATA%\LaunchPad\config.json` (with UWP virtualization stripped).

**Request:**

| Key          | Type   | Required | Description                                                |
|--------------|--------|----------|------------------------------------------------------------|
| `action`     | string | yes      | `"load-config"`                                            |
| `configPath` | string | no       | Override path to config file. Defaults to standard location. |

**Response:**

| Key          | Type   | Condition                      | Description                                          |
|--------------|--------|--------------------------------|------------------------------------------------------|
| `status`     | string | always                         | `"success"`, `"filenotfound"`, or `"parseerror"`     |
| `configPath` | string | always                         | The resolved config file path that was used          |
| `json`       | string | when status is `"success"`     | Serialized `LaunchPadConfig` as compact JSON         |
| `error`      | string | when status is `"parseerror"`  | JSON parse error message                             |

The `status` values map to the `ConfigLoadStatus` enum (`Success`, `FileNotFound`, `ParseError`), lowercased via `ToString().ToLowerInvariant()`.

**Example request (default path):**

```
ValueSet { ["action"] = "load-config" }
```

**Example request (custom path):**

```
ValueSet {
    ["action"]     = "load-config",
    ["configPath"] = "D:\\configs\\launchpad.json"
}
```

**Example success response:**

```
ValueSet {
    ["status"]     = "success",
    ["configPath"] = "C:\\Users\\user\\AppData\\Local\\LaunchPad\\config.json",
    ["json"]       = "{\"items\":[{\"name\":\"Notepad\",\"type\":\"Exe\",\"path\":\"notepad.exe\",\"args\":null,\"icon\":null}]}"
}
```

**Example file-not-found response:**

```
ValueSet {
    ["status"]     = "filenotfound",
    ["configPath"] = "C:\\Users\\user\\AppData\\Local\\LaunchPad\\config.json"
}
```

**Example parse-error response:**

```
ValueSet {
    ["status"]     = "parseerror",
    ["configPath"] = "C:\\Users\\user\\AppData\\Local\\LaunchPad\\config.json",
    ["error"]      = "'i' is an invalid start of a value. LineNumber: 0 | BytePositionInLine: 0."
}
```

**Widget client:** `CompanionClient.LoadConfigAsync()` returns a tuple `(ConfigLoadStatus Status, LaunchPadConfig? Config, string? ConfigPath, string? Error)`.

**Config JSON schema:**

```json
{
  "items": [
    {
      "name": "Display name",
      "type": "Exe" | "Url" | "Store",
      "path": "full path or URL",
      "args": "optional arguments",
      "icon": "optional icon path"
    }
  ]
}
```

---

### `open-editor`

Widget requests the companion to open the WPF config editor window.

**Request:**

| Key          | Type   | Required | Description                           |
|--------------|--------|----------|---------------------------------------|
| `action`     | string | yes      | `"open-editor"`                       |
| `configPath` | string | yes      | Path to the config file to edit       |

**Response:**

| Key      | Type   | Condition | Description          |
|----------|--------|-----------|----------------------|
| `status` | string | always    | `"ok"` or `"error"`  |

If the editor is already open, the companion focuses the existing window instead of opening a second one.

**Example request:**

```
ValueSet {
    ["action"]     = "open-editor",
    ["configPath"] = "C:\\Users\\user\\AppData\\Local\\LaunchPad\\config.json"
}
```

**Example response:**

```
ValueSet { ["status"] = "ok" }
```

**Widget client:** `CompanionClient.OpenEditorAsync(string configPath)` returns `bool` (`true` on success).

---

### `config-updated`

Companion notifies the widget that config has been saved from the editor.

**Direction:** Companion → Widget (unsolicited push message)

This is a bidirectional use of the `AppServiceConnection` — the companion initiates the message, not the widget.

**Message:**

| Key      | Type   | Description                          |
|----------|--------|--------------------------------------|
| `action` | string | `"config-updated"`                   |

**Example message:**

```
ValueSet { ["action"] = "config-updated" }
```

The widget handles this by calling `LoadConfigAsync()` to refresh the grid with the updated configuration.

---

## Key Flows

### Startup and Connection

```
Widget Process                          Companion Process
     |                                        |
     |  (Game Bar activates widget)           |
     |  OnActivated (protocol activation)     |
     |  Creates Frame, navigates to widget    |
     |                                        |
     |                         (MSIX launches companion as full-trust process)
     |                                        |
     |                                  Acquires mutex
     |                                  Creates AppServiceConnection
     |                                  Calls OpenAsync()
     |                                        |
     |  OnBackgroundActivated                 |
     |  Stores AppServiceConnection    <------+
     |  Sets App.CompanionConnection          |
     |                                  Blocks on ManualResetEvent
     |                                        |
     |  (widget UI ready, connection live)    |
```

### Launch Flow

```
Widget UI                CompanionClient              Companion
   |                           |                          |
   | User clicks tile          |                          |
   |  LaunchAsync(type, path)  |                          |
   |-------------------------->|                          |
   |                           | SendMessageAsync         |
   |                           |  { action: "launch",     |
   |                           |    type, path, args }    |
   |                           |------------------------->|
   |                           |                          | LaunchHandler.Launch()
   |                           |                          | Process.Start()
   |                           |                          |
   |                           |    { status: "ok" }      |
   |                           |<-------------------------|
   |           true            |                          |
   |<--------------------------|                          |
```

### Config Load Flow

```
Widget UI                CompanionClient              Companion
   |                           |                          |
   | Page loaded               |                          |
   |  LoadConfigAsync()        |                          |
   |-------------------------->|                          |
   |                           | SendMessageAsync         |
   |                           |  { action: "load-config" }
   |                           |------------------------->|
   |                           |                          | ConfigLoader.Load()
   |                           |                          | Read & parse JSON
   |                           |                          |
   |                           | { status: "success",     |
   |                           |   configPath: "...",     |
   |                           |   json: "{...}" }        |
   |                           |<-------------------------|
   |                           | Deserialize JSON         |
   | (Status, Config,         |                          |
   |  ConfigPath, Error)       |                          |
   |<--------------------------|                          |
```

### Open Editor Flow

```
Widget UI                CompanionClient              Companion
   |                           |                          |
   | User clicks gear button   |                          |
   |  OpenEditorAsync(path)    |                          |
   |-------------------------->|                          |
   |                           | SendMessageAsync         |
   |                           |  { action: "open-editor",|
   |                           |    configPath: "..." }   |
   |                           |------------------------->|
   |                           |                          | Opens WPF ConfigEditorWindow
   |                           |                          | (or focuses existing window)
   |                           |  { status: "ok" }        |
   |                           |<-------------------------|
   |           true            |                          |
   |<--------------------------|                          |
   |                           |                          |
   |                           |       (user edits config in editor)
   |                           |                          |
   |                           |       User clicks "Save & Refresh"
   |                           |                          | ConfigLoader.Save(path, config)
   |                           |                          |
   |   { action: "config-updated" }                       |
   |<-----------------------------------------------------|
   |                           |                          |
   | LoadConfigAsync()         |                          |
   | (refresh grid)            |                          |
```

## File Paths

| Path                                           | Purpose                        |
|------------------------------------------------|--------------------------------|
| `%LOCALAPPDATA%\LaunchPad\config.json`         | Default configuration file     |
| `%LOCALAPPDATA%\LaunchPad\icons\`              | Cached icon PNGs               |
| `%LOCALAPPDATA%\LaunchPad\icons\<hash>.png`    | Individual cached icon (SHA-256 hash of source path/URL, first 16 hex chars) |

## Source Files

| File                                              | Role                                |
|---------------------------------------------------|-------------------------------------|
| `LaunchPad.Widget/App.xaml.cs`                    | Connection setup (server/receiver)  |
| `LaunchPad.Widget/Services/CompanionClient.cs`    | Widget-side request helpers         |
| `LaunchPad.Companion/Program.cs`                  | Connection setup + action dispatch  |
| `LaunchPad.Companion/LaunchHandler.cs`            | Process launch logic                |
| `LaunchPad.Companion/IconExtractor.cs`            | Icon extraction and favicon fetch   |
| `LaunchPad.Companion/ExePicker.cs`                | File picker dialog and config append|
| `LaunchPad.Shared/ConfigModels.cs`                | Config types, loader, and saver     |
| `LaunchPad.Package/Package.appxmanifest`          | App Service declaration             |

## See Also

- [Architecture](ARCHITECTURE.md) -- system overview and project structure
- [Configuration](CONFIG.md) -- config file schema loaded by the `load-config` action
- [Deployment](DEPLOYMENT.md) -- App Service manifest setup and MSIX packaging
