# WPF Config Editor

## Problem

The widget's "+" button sends an IPC message to the companion to show a Win32 file picker, but Game Bar's fullscreen overlay blocks the dialog from appearing or receiving focus. Configuration editing needs to happen outside the Game Bar overlay.

## Solution

Replace the file-picker-only add flow with a full WPF config editor window hosted inside the companion process. The widget's "+" button becomes an "open editor" button. The editor provides full CRUD (add, remove, edit, reorder) for launch items.

## Scope

- **Item types supported**: EXE and URL (store items deferred)
- **Operations**: Add, remove, edit all fields, reorder
- **Launch**: Widget sends `open-editor` IPC action, companion opens WPF window on STA thread
- **Persistence**: Editor reads/writes config.json directly via `ConfigLoader`. "Save & Refresh" button writes config and notifies the widget to reload via IPC.

## Architecture

**Approach: Editor inside the companion process.** Add `<UseWPF>true</UseWPF>` to the companion csproj. The WPF window runs on a dedicated STA thread (same pattern as the existing `ExePicker.ShowPickerDialog()`). Single-instance — if the editor is already open, bring it to the foreground.

No new projects or processes. The editor has direct access to `ConfigLoader`, `ExePicker`, and `IconExtractor`.

## UI Layout

**Window**: ~500x450, dark theme matching the widget palette (#25282C background family).

**List-detail layout**:
- **Left panel** (~180px): Scrollable item list showing icon thumbnail, name, and type badge. Click to select. Selected item highlighted with accent border.
- **Right panel**: Edit form for the selected item. Fields adapt by type.
- **Toolbar** (top): "Add EXE" button (opens file picker, adds result to list), "Add URL" button (adds blank URL entry to list), up/down reorder buttons, remove button.
- **Bottom bar**: Item count label (left), "Save & Refresh" button (right).

### Edit Panel Fields

**EXE items**:
| Field | Control | Notes |
|-------|---------|-------|
| Name | TextBox | Pre-filled from `FileVersionInfo.FileDescription` on add |
| Type | Read-only label | "exe" — set at creation, not editable |
| Path | TextBox + Browse button | Browse opens `OpenFileDialog` |
| Arguments | TextBox | Optional, placeholder "Optional command-line args" |
| Custom Icon | TextBox + Browse button | Optional, placeholder "Auto-detected" |

**URL items**:
| Field | Control | Notes |
|-------|---------|-------|
| Name | TextBox | User enters manually |
| Type | Read-only label | "url" — set at creation, not editable |
| Path | TextBox | Full URL including scheme |
| Custom Icon | TextBox + Browse button | Optional, placeholder "Auto-detected" |

### Dark Theme Colors

Reuse the widget's palette where applicable:
- Window background: `#1e1e1e`
- Panel backgrounds: `#252526` (list, toolbar, bottom bar), `#2d2d2d` (toolbar)
- Input backgrounds: `#3c3c3c`
- Borders: `#555`
- Text: `#e0e0e0` (primary), `#888` (secondary/labels)
- Accent: `#0e639c` (buttons, selection highlight)
- Danger: `#5a1d1d` (remove button)

## IPC Changes

### New action: `open-editor`

**Widget → Companion (request)**:
```
{ "action": "open-editor", "configPath": "<resolved path>" }
```

**Companion → Widget (response)**:
```
{ "status": "ok" }
```

The companion opens the editor window (or focuses it if already open) and immediately responds `ok`. The editor operates independently from this point.

### New notification: `config-updated`

**Companion → Widget (unsolicited message)**:
```
{ "action": "config-updated" }
```

Sent when the user clicks "Save & Refresh" in the editor. The widget receives this via a `RequestReceived` handler on the `AppServiceConnection` and calls `LoadConfigAsync()` to reload the grid.

This is a new pattern — the companion initiates a message to the widget. The `AppServiceConnection` supports bidirectional messaging; the widget just needs a `RequestReceived` handler registered on the connection.

### Removed action: `add-exe`

The `add-exe` IPC action and `CompanionClient.AddExeAsync` are removed. The `ExePicker` class is retained (the editor calls it directly for the "Add EXE" file picker).

## File Changes

### Modified files
| File | Change |
|------|--------|
| `LaunchPad.Companion/LaunchPad.Companion.csproj` | Add `<UseWPF>true</UseWPF>` |
| `LaunchPad.Companion/Program.cs` | Add `open-editor` dispatcher case, STA thread + single-instance management, store connection reference for `config-updated` notifications, remove `HandleAddExe` |
| `LaunchPad.Widget/LaunchPadWidget.xaml` | Change button content from "+" to a gear glyph (⚙ or Segoe MDL2 `\uE713`), update tooltip to "Edit configuration" |
| `LaunchPad.Widget/LaunchPadWidget.xaml.cs` | Rename `OnAddClick` → `OnEditClick`, send `open-editor` instead of `add-exe` |
| `LaunchPad.Widget/Services/CompanionClient.cs` | Replace `AddExeAsync` with `OpenEditorAsync`, add `RequestReceived` handler for `config-updated` |
| `LaunchPad.Widget/App.xaml.cs` | Wire `RequestReceived` handler on companion connection |
| `docs/IPC.md` | Document `open-editor` and `config-updated`, remove `add-exe` |
| `docs/UI.md` | Update button description and behavior |

### New files
| File | Purpose |
|------|---------|
| `LaunchPad.Companion/Editor/EditorWindow.xaml` | WPF window XAML — dark theme, list-detail layout |
| `LaunchPad.Companion/Editor/EditorWindow.xaml.cs` | Code-behind — item selection, add/remove/reorder, save, file picker integration |

### Unchanged files
| File | Reason |
|------|--------|
| `LaunchPad.Companion/ExePicker.cs` | Editor calls `ShowPickerDialog()` and `GetDisplayName()` directly |
| `LaunchPad.Companion/IconExtractor.cs` | Editor may use for icon preview in the list |
| `LaunchPad.Shared/ConfigModels.cs` | Editor uses `ConfigLoader.Load/Save` directly |

## Behaviors

- **Single instance**: If the editor is already open when `open-editor` arrives, bring the existing window to the foreground. Do not open a second window.
- **Add EXE flow**: Click "Add EXE" → file picker dialog → on selection, `GetDisplayName()` extracts the name → new item appended to list and selected → user can edit fields before saving.
- **Add URL flow**: Click "Add URL" → blank URL item appended to list and selected → user fills in name and URL.
- **Reorder**: Up/down buttons swap the selected item with its neighbor. Disabled at list boundaries.
- **Remove**: Removes the selected item. Selects the next item (or previous if last was removed).
- **Save & Refresh**: `ConfigLoader.Save()` writes config.json, then companion sends `{ "action": "config-updated" }` to widget via `AppServiceConnection.SendMessageAsync()`.
- **Editor close**: No special handling. The editor can be closed and reopened freely. Unsaved changes are lost (no dirty-state warning for v1).
