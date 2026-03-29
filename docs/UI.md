# LaunchPad UI Documentation

This document describes the UI architecture, theming, layout, interactive behaviors, and activation flow for the LaunchPad Xbox Game Bar widget.

---

## Dark Theme Approach

LaunchPad uses a hardcoded dark theme following the ToothNClaw Game Bar widget pattern:

- `App.xaml` sets `RequestedTheme="Dark"` at the Application level.
- `LaunchPadWidget.xaml` sets `Background="#202020"` directly on the Page element and repeats `RequestedTheme="Dark"`.
- There is **no** Game Bar theme event handling. The widget does not subscribe to `XboxGameBarWidget.ThemeChanged`. The dark appearance is entirely self-managed.
- The widget **does** subscribe to `XboxGameBarWidget.RequestedOpacityChanged` to honor the Game Bar's opacity slider (see Opacity Support below).
- This avoids flickering and inconsistencies that can occur when trying to dynamically match the Game Bar overlay theme.

---

## Opacity Support

The widget subscribes to `XboxGameBarWidget.RequestedOpacityChanged` and applies the requested opacity to the page background's alpha channel:

1. On widget initialization, the handler is registered and the current opacity is applied.
2. When the Game Bar opacity slider changes, `RequestedOpacityChanged` fires.
3. The handler converts `widget.RequestedOpacity` (0–100 scale) to an alpha byte and rebuilds the page `Background` brush with `Color.FromArgb(alpha, 0x20, 0x20, 0x20)`.

Only the background becomes transparent — text, icons, and tile content stay fully opaque. Setting `Page.Opacity` directly would wash out all content, so the alpha channel approach is used instead.

---

## Color Palette

All colors are defined as `SolidColorBrush` resources in `LaunchPadWidget.xaml` `Page.Resources`, except the page background which is set inline.

| Token                          | Hex          | Usage                                      |
|--------------------------------|--------------|--------------------------------------------|
| Page `Background`              | `#202020`    | Page/widget background                     |
| `TileBackground`               | `#2D2D2D`   | Default tile background                    |
| `TileBackgroundHover`          | `#383838`   | Tile background on pointer hover           |
| `TileBackgroundPressed`        | `#252525`   | Tile background when pressed               |
| `LaunchSuccessBrush`           | `#4D107C10` | Launch success overlay (Xbox green at 30%) |
| `LaunchFailureBrush`           | `#4DC42B1C` | Launch failure overlay (red at 30%)        |
| `ButtonBackground`             | `#2D2D2D`   | Edit button resting background             |
| `ButtonBackgroundPointerOver`  | `#383838`   | Edit button hover background               |
| `ButtonBackgroundPressed`      | `#252525`   | Edit button pressed background             |
| `ButtonBorderBrush`            | `#1FFFFFFF` | Edit button border (12% white)             |
| `ButtonBorderBrushPointerOver` | `#1FFFFFFF` | Edit button border on hover (12% white)    |
| `ButtonBorderBrushPressed`     | `#14FFFFFF` | Edit button border when pressed            |
| Secondary text foreground      | `#C5FFFFFF` | Empty state message text (77% white)       |

Click feedback uses `LaunchSuccessBrush` / `LaunchFailureBrush` overlays that fade out over 400ms.

---

## XAML Structure

The page layout follows this hierarchy:

```
Page (Background=#202020, RequestedTheme=Dark)
  Page.Resources (brush definitions)
  Grid (Padding=8)
    ScrollViewer "ItemsScrollViewer"  -- vertical scrolling for overflow
      GridView "ItemsGrid"           -- centered tile grid, XYFocusKeyboardNavigation
        ItemsWrapGrid                -- 4-column horizontal wrap, centered
        DataTemplate                 -- per-tile template (LaunchItem)
    Button "EditButton"              -- circular gear button, bottom-right
    StackPanel "EmptyState"          -- centered message, Visibility=Collapsed
```

### GridView (ItemsGrid)

- Bound to `Items` via `{x:Bind Items}` (an `ObservableCollection<LaunchItem>` on the page).
- `SelectionMode="Single"` with `SingleSelectionFollowsFocus="True"` -- the focused tile is also the selected tile, providing a clear visual indicator for keyboard/controller navigation.
- `IsItemClickEnabled="True"` with handler `OnItemClick`.
- `XYFocusKeyboardNavigation="Enabled"` for reliable arrow key and D-pad navigation.
- `TabNavigation="Once"` -- Tab enters the grid, arrow keys navigate within it.

### ItemsWrapGrid (GridView.ItemsPanel)

- `MaximumRowsOrColumns="4"` -- enforces a 4-column grid.
- `Orientation="Horizontal"` -- items flow left-to-right, wrapping to new rows.
- `ItemWidth="88"` / `ItemHeight="88"` -- each cell occupies 88x88 device-independent pixels.

---

## Tile Template

Each tile is defined inside `GridView.ItemTemplate` as a `DataTemplate` with `x:DataType="models:LaunchItem"`.

```
Grid "TileRoot" (80x80, CornerRadius=8, Padding=4, Background=TileBackground)
  RenderTransform: CompositeTransform (RenderTransformOrigin=0.5,0.5)
  Border "FocusBorder" (CornerRadius=8, TileFocusBorder, Opacity=0, keyboard/controller focus indicator)
  Border "FeedbackOverlay" (CornerRadius=8, Opacity=0, overlay for launch feedback)
  StackPanel (centered, Spacing=4)
    Image (36x36, Stretch=Uniform, Source bound to IconSource)
    TextBlock (FontSize=12, centered, CharacterEllipsis, MaxLines=1)
```

Key properties:
- **Tile size**: 80x80 within an 88x88 cell, giving 4px implicit margin per side.
- **Corner radius**: 8px rounded corners.
- **CompositeTransform**: Enables scale animations on press (RenderTransformOrigin centered at 0.5,0.5).
- **FeedbackOverlay**: A `Border` layered behind content, used for success/failure color feedback animations.
- **Icon**: 36x36, uniform stretch, bound to `LaunchItem.IconSource` with `Mode=OneWay` to pick up async icon loads.
- **Label**: `FontSize="12"`, single line, `TextTrimming="CharacterEllipsis"` for overflow, `TextAlignment="Center"`.

---

## Interactive States

### Hover

Pointer events animate the tile background over 150ms (was instant swap):

- `PointerEntered` handler: animates `TileRoot.Background` to `TileBackgroundHover` (`#383838`) over 150ms using a `ColorAnimation` storyboard.
- `PointerExited` handler: animates back to `TileBackground` (`#2D2D2D`) over 150ms.

### Pressed

Pointer press adds a scale-down effect:

- `PointerPressed` handler: animates background to `TileBackgroundPressed` (`#252525`) and scales the tile to 0.95 via `CompositeTransform.ScaleX`/`ScaleY` over 100ms.
- `PointerReleased` / `PointerExited`: restores scale to 1.0 and returns to the appropriate background state.

### Launch and Dismiss

On `ItemClick`, the widget launches the target app and dismisses the Game Bar overlay:

1. **URL and Store items** use `XboxGameBarWidget.LaunchUriAsync(uri)` — Game Bar's built-in launcher that handles overlay dismissal and app focus automatically.
2. **EXE items without arguments** also use `LaunchUriAsync` with the file path as a URI.
3. **EXE items with arguments** (or if `LaunchUriAsync` fails) fall back to the companion process via the `launch` IPC action. The companion calls `SetForegroundWindow` on the launched process to bring it to the foreground.
4. When the widget is pinned, no overlay dismissal occurs — the widget stays visible.

### Click Feedback

After the launch completes, the `FeedbackOverlay` border flashes:

1. The handler locates the `GridViewItem` container via `gridView.ContainerFromItem(item)`.
2. It finds the `FeedbackOverlay` border inside the tile template.
3. The overlay's `Background` is set to `LaunchSuccessBrush` or `LaunchFailureBrush`.
4. The overlay's `Opacity` is animated from 1 to 0 over 400ms using a `DoubleAnimation` storyboard.

Note: In overlay mode, the feedback animation may not be visible since `LaunchUriAsync` dismisses Game Bar immediately.

### Controller / Keyboard Focus

Tiles are focusable for gamepad and keyboard navigation:

- `GridView` uses `SelectionMode="Single"` with `SingleSelectionFollowsFocus="True"` — the focused tile is always the selected tile.
- Custom focus visual: a `FocusBorder` element inside each tile (2px `TileFocusBorder` border, `#60FFFFFF`) shows/hides via `GotFocus`/`LostFocus` handlers. System focus visuals are disabled.
- Focused tiles also get the hover background color for additional visual emphasis.
- On first load, `SelectedIndex` is set to 0 and the first tile receives keyboard focus.
- All tile colors are read from XAML resources via `GetResourceColor()` — no hardcoded values in code-behind.

---

## Edit Button

A circular floating action button for opening the config editor:

| Property            | Value                                       |
|---------------------|---------------------------------------------|
| Content             | Gear glyph (`\uE713`)                       |
| FontFamily          | Segoe MDL2 Assets                            |
| FontSize            | 16                                           |
| Width / Height      | 40 x 40                                      |
| CornerRadius        | 20 (fully circular)                          |
| HorizontalAlignment | Right                                        |
| VerticalAlignment   | Bottom                                       |
| Margin              | `0,0,8,8` (8px from right/bottom)            |
| ToolTip             | "Edit configuration"                         |

**Behavior** (`OnEditClick`):
1. The button is disabled (`EditButton.IsEnabled = false`) to prevent double-clicks during the IPC call.
2. Sends the `open-editor` IPC action to the companion, which opens the WPF config editor window.
3. The button is re-enabled in a `finally` block after the IPC call completes.

---

## Config Editor

The config editor (`EditorWindow`) is a WPF window using MVVM architecture with a WinUI 3 card-based design.

### Architecture

- **EditorViewModel** — main view model with `ObservableCollection<ItemViewModel>`, commands for add/edit/delete/move/save, and overlay dialog state
- **ItemViewModel** — wraps `LaunchItemConfig` with `INotifyPropertyChanged` and async icon loading
- **EditorModel** — data layer (load, save, add, remove, move, validate) — unchanged from pre-redesign, fully tested
- **EditorTheme.xaml** — shared resource dictionary with all colors, brushes, and styles (merged by both editor and picker windows)

### Layout

Single scrollable page with:
- Page title and subtitle
- Section header "Items"
- Vertical stack of card-style item rows (`ItemsControl` bound to `ObservableCollection`)
- Each card shows: 32x32 icon, name, path, and icon buttons (move up/down, edit, delete)
- "+ Add item" menu (EXE Application / URL / Store App)
- Item count and "Save and Refresh" button at the bottom

### Edit Dialog

Clicking edit on a card opens an overlay dialog within the same window:
- Semi-transparent backdrop dims the item list
- Centered card with form fields: Name, Type (read-only), Path (with Browse for EXE), Arguments (EXE only), Custom Icon (with Browse)
- Save commits changes to the item; Cancel or backdrop click discards
- Editing the custom icon path triggers an async icon reload on the card

### Validation

On save, `EditorModel.Validate()` checks for empty names, empty paths, URLs without `http://`/`https://` scheme, and Store paths without `shell:AppsFolder\` prefix. If issues are found, a warning dialog shows them with a "Save anyway?" option.

### Theme

All colors and styles are defined in `EditorTheme.xaml`:
- `PageBackground` (`#202020`), `CardBackground` (`#2D2D2D`), `CardBorder` (`#15FFFFFF`)
- `TextPrimary` (white), `TextSecondary` (`#99FFFFFF`), `TextTertiary` (`#66FFFFFF`)
- Styles: `SettingsCard`, `DialogCard`, `IconButton`, `FieldLabel`, `PageTitle`, `SectionHeader`

## Store App Picker

The editor includes a "Select Store App" picker dialog (`StoreAppPickerWindow`) for adding Store/UWP apps.

- Opened via the "+ Add item" menu → "Store App"
- Lists all installed Store/UWP apps using `PackageManager.FindPackagesForUser`
- Each entry shows a 32x32 icon (from the package manifest) and app name
- Search box filters by app name (case-insensitive substring match)
- Selection via double-click or OK button
- Selected app is added with `Type = Store` and `Path = shell:AppsFolder\{AUMID}`
- Uses the same `EditorTheme.xaml` for consistent visuals

---

## Empty and Error States

The `EmptyState` StackPanel is shown when the grid cannot display tiles. It is centered both vertically and horizontally with `Spacing="8"` and `Padding="16"`.

### EmptyStateTitle

- `FontSize="14"`, `FontWeight="SemiBold"`, centered.
- Default text: "No apps configured".

### EmptyStateMessage

- `FontSize="11"`, centered, `TextWrapping="Wrap"`.
- `Foreground="#C5FFFFFF"` (secondary/muted text, 77% white).

### Trigger Conditions

The `ShowEmptyState(title, message)` method hides `ItemsGrid` and shows `EmptyState`. It is called from `LoadConfigAsync()` under three conditions:

| Condition                     | Title                   | Message                                          |
|-------------------------------|-------------------------|--------------------------------------------------|
| `ConfigLoadStatus.FileNotFound` | "No apps configured"    | "Click the gear button to add apps"              |
| `ConfigLoadStatus.ParseError`   | "Invalid config file"   | "JSON parse error:\n{error}"                     |
| Config is null or has 0 items   | "No apps configured"    | "Click the gear button to add apps"              |

When items are loaded successfully, `ItemsGrid.Visibility` is set to `Visible` and `EmptyState.Visibility` to `Collapsed`.

---

## Widget Window Sizing

Configured in `Package.appxmanifest` under `GameBarWidget > Window > Size`:

| Dimension  | Value (px) |
|------------|------------|
| Width      | 400        |
| Height     | 350        |
| MinWidth   | 300        |
| MinHeight  | 250        |
| MaxWidth   | 600        |
| MaxHeight  | 500        |

Resizing is enabled in both directions:
- `Horizontal`: true
- `Vertical`: true

The widget is marked `PinningSupported`, `HomeMenuVisible`, `ActivateAfterInstall`, and `FavoriteAfterInstall`.

At the default 400x350 size with 8px grid padding, the 4-column layout (4 x 88px = 352px) fits comfortably. At MinWidth 300, some horizontal scrolling or reflow may occur.

---

## Game Bar Activation Flow

Defined in `App.xaml.cs`.

### OnLaunched (direct launch)

```
protected override void OnLaunched(LaunchActivatedEventArgs args)
{
    Current.Exit();
}
```

The widget is not a standalone app. If launched directly (e.g., from Start menu), it exits immediately. It can only be activated through the Game Bar protocol.

### OnActivated (protocol activation)

1. Checks `args.Kind == ActivationKind.Protocol`.
2. Verifies the URI scheme is `ms-gamebarwidget`.
3. Casts to `XboxGameBarWidgetActivatedEventArgs` and checks `IsLaunchActivation`.
4. Creates a `Frame`, sets it as `Window.Current.Content`.
5. Instantiates `XboxGameBarWidget` with the activation args, `CoreWindow`, and the frame.
6. Navigates the frame to `LaunchPadWidget` page.
7. Calls `Window.Current.Activate()`.

### OnBackgroundActivated (App Service)

When the companion process connects via the `com.launchpad.service` App Service:

1. Captures the `AppServiceConnection` from `AppServiceTriggerDetails`.
2. Stores it in both a private field and the static `App.CompanionConnection` property for use by `CompanionClient`.
3. Registers a cancellation handler that completes the deferral and nulls out the connection.

### OnSuspending

Nulls the widget and companion connection references, then completes the deferral.

---

## LaunchItem Model

**File**: `LaunchPad.Widget\Models\LaunchItem.cs`

Implements `INotifyPropertyChanged` for data binding.

| Property       | Type           | Binding     | Description                                     |
|----------------|----------------|-------------|-------------------------------------------------|
| `Name`         | `string`       | `x:Bind`    | Display label shown under the icon               |
| `Type`         | `string`       | --          | Item type: `"exe"`, `"url"`, or `"store"`        |
| `Path`         | `string`       | --          | Launch target (file path, URL, or store URI)     |
| `Args`         | `string?`      | --          | Optional command-line arguments (EXE only)       |
| `CustomIconPath` | `string?`    | --          | Optional user-specified icon file path           |
| `IconSource`   | `BitmapImage?` | `OneWay`    | Resolved icon for display; raises PropertyChanged |

The `IconSource` property is the only one with change notification, since icons are loaded asynchronously after the item is added to the collection. The `Image` element in the tile template binds to it with `Mode=OneWay`.

### Icon Resolution Order

In `LoadIconsAsync()`, icons are resolved per item in priority order:

1. **Custom icon** -- If `CustomIconPath` is set, call `CompanionClient.LoadCustomIconAsync(iconPath)` to load the image file as base64 PNG data via the companion process.
2. **Type-based** -- If no custom icon:
   - `Type == "exe"`: call `CompanionClient.ExtractIconAsync(path)` to extract the EXE's embedded icon.
   - `Type == "url"`: call `CompanionClient.FetchFaviconAsync(path)` to download the site's favicon.
3. **Default fallback** -- On failure or null, fall back to a default asset:
   - `"url"` type: `ms-appx:///Assets/DefaultGlobe.png`
   - All others: `ms-appx:///Assets/DefaultApp.png`

### Data Binding

The page exposes `Items` as a public property:

```csharp
public ObservableCollection<LaunchItem> Items { get; } = new();
```

The `GridView.ItemsSource` binds to this via `{x:Bind Items}`. Because it is an `ObservableCollection`, adding or removing items automatically updates the grid without manual refresh.

---

## File Reference

| File                                         | Role                                   |
|----------------------------------------------|-----------------------------------------|
| `LaunchPad.Widget\LaunchPadWidget.xaml`       | XAML layout, resources, data templates   |
| `LaunchPad.Widget\LaunchPadWidget.xaml.cs`    | Code-behind: loading, events, feedback   |
| `LaunchPad.Widget\App.xaml`                   | Application theme (`RequestedTheme=Dark`)|
| `LaunchPad.Widget\App.xaml.cs`                | Activation, App Service, lifecycle       |
| `LaunchPad.Widget\Models\LaunchItem.cs`       | Tile data model with INotifyPropertyChanged |
| `LaunchPad.Package\Package.appxmanifest`      | Widget registration, window sizing       |

## See Also

- [Architecture](ARCHITECTURE.md) -- system overview and UWP sandbox constraints that shape the UI
- [Configuration](CONFIG.md) -- config schema that drives what tiles are displayed in the grid
- [IPC Protocol](IPC.md) -- message protocol used when tile clicks trigger app launches
