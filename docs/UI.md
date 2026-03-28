# LaunchPad UI Documentation

This document describes the UI architecture, theming, layout, interactive behaviors, and activation flow for the LaunchPad Xbox Game Bar widget.

---

## Dark Theme Approach

LaunchPad uses a hardcoded dark theme following the ToothNClaw Game Bar widget pattern:

- `App.xaml` sets `RequestedTheme="Dark"` at the Application level.
- `LaunchPadWidget.xaml` sets `Background="#25282C"` directly on the Page element and repeats `RequestedTheme="Dark"`.
- There is **no** Game Bar opacity or theme event handling. The widget does not subscribe to `XboxGameBarWidget.ThemeChanged` or `XboxGameBarWidget.OpacityChanged`. The dark appearance is entirely self-managed.
- This avoids flickering and inconsistencies that can occur when trying to dynamically match the Game Bar overlay theme.

---

## Color Palette

All colors are defined as `SolidColorBrush` resources in `LaunchPadWidget.xaml` `Page.Resources`, except the page background which is set inline.

| Token                          | Hex       | Usage                                      |
|--------------------------------|-----------|----------------------------------------------|
| Page `Background`              | `#25282C` | Page/widget background                       |
| `TileBackground`               | `#30343A` | Default tile background                      |
| `TileBackgroundHover`          | `#3E434B` | Tile background on pointer hover              |
| `ButtonBackground`             | `#3E434B` | Add button resting background                 |
| `ButtonBackgroundPointerOver`  | `#484E58` | Add button hover background                   |
| `ButtonBackgroundPressed`      | `#3A3F47` | Add button pressed background                 |
| `ButtonBorderBrush`            | `#6B7584` | Add button border (resting and hover)         |
| `ButtonBorderBrushPointerOver` | `#6B7584` | Add button border on hover (same as resting)  |
| `ButtonBorderBrushPressed`     | `#555D69` | Add button border when pressed                |
| Secondary text foreground      | `#A1A3A5` | Empty state message text (`EmptyStateMessage`) |

Click feedback colors (set in code-behind, not in XAML resources):
- **Success flash**: `Windows.UI.Colors.Green` at `Opacity = 0.3`
- **Failure flash**: `Windows.UI.Colors.Red` at `Opacity = 0.3`

---

## XAML Structure

The page layout follows this hierarchy:

```
Page (Background=#25282C, RequestedTheme=Dark)
  Page.Resources (brush definitions)
  Grid (Padding=8)
    GridView "ItemsGrid"         -- main tile grid, Visibility=Visible
      ItemsWrapGrid              -- 4-column horizontal wrap
      DataTemplate               -- per-tile template (LaunchItem)
    Button "EditButton"          -- circular gear button, bottom-right
    StackPanel "EmptyState"      -- centered message, Visibility=Collapsed
      TextBlock "EmptyStateTitle"
      TextBlock "EmptyStateMessage"
```

### GridView (ItemsGrid)

- Bound to `Items` via `{x:Bind Items}` (an `ObservableCollection<LaunchItem>` on the page).
- `SelectionMode="None"` -- tiles are not selectable, only clickable.
- `IsItemClickEnabled="True"` with handler `OnItemClick`.

### ItemsWrapGrid (GridView.ItemsPanel)

- `MaximumRowsOrColumns="4"` -- enforces a 4-column grid.
- `Orientation="Horizontal"` -- items flow left-to-right, wrapping to new rows.
- `ItemWidth="88"` / `ItemHeight="88"` -- each cell occupies 88x88 device-independent pixels.

---

## Tile Template

Each tile is defined inside `GridView.ItemTemplate` as a `DataTemplate` with `x:DataType="models:LaunchItem"`.

```
Grid (80x80, CornerRadius=8, Padding=4, Background=TileBackground)
  StackPanel (centered, Spacing=4)
    Image (36x36, Stretch=Uniform, Source bound to IconSource)
    TextBlock (FontSize=11, centered, CharacterEllipsis, MaxLines=1)
```

Key properties:
- **Tile size**: 80x80 within an 88x88 cell, giving 4px implicit margin per side.
- **Corner radius**: 8px rounded corners.
- **Icon**: 36x36, uniform stretch, bound to `LaunchItem.IconSource` with `Mode=OneWay` to pick up async icon loads.
- **Label**: `FontSize="11"`, single line, `TextTrimming="CharacterEllipsis"` for overflow, `TextAlignment="Center"`.

---

## Interactive States

### Hover

Pointer events are handled on each tile's root `Grid`:

- `PointerEntered` handler `OnTilePointerEntered`: sets `grid.Background` to `TileBackgroundHover` (`#3E434B`).
- `PointerExited` handler `OnTilePointerExited`: resets `grid.Background` to `TileBackground` (`#30343A`).

These are defined in the code-behind (`LaunchPadWidget.xaml.cs`, lines 171-181) by casting the `sender` to `Grid` and swapping the brush from page resources.

### Click Feedback

On `ItemClick`, after the companion process reports launch success or failure, the clicked tile flashes briefly:

1. The handler locates the `GridViewItem` container via `gridView.ContainerFromItem(item)`.
2. It finds the inner `Grid` using a recursive `FindChild<Grid>()` helper that walks the visual tree.
3. The grid's `Background` is temporarily replaced:
   - **Success**: `SolidColorBrush(Colors.Green)` at `Opacity = 0.3`
   - **Failure**: `SolidColorBrush(Colors.Red)` at `Opacity = 0.3`
4. After a 200ms `Task.Delay`, the original brush is restored.

This provides non-intrusive confirmation without animation storyboards.

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

## Empty and Error States

The `EmptyState` StackPanel is shown when the grid cannot display tiles. It is centered both vertically and horizontally with `Spacing="8"` and `Padding="16"`.

### EmptyStateTitle

- `FontSize="14"`, `FontWeight="SemiBold"`, centered.
- Default text: "No apps configured".

### EmptyStateMessage

- `FontSize="11"`, centered, `TextWrapping="Wrap"`.
- `Foreground="#A1A3A5"` (secondary/muted text).

### Trigger Conditions

The `ShowEmptyState(title, message)` method hides `ItemsGrid` and shows `EmptyState`. It is called from `LoadConfigAsync()` under three conditions:

| Condition                     | Title                   | Message                                          |
|-------------------------------|-------------------------|--------------------------------------------------|
| `ConfigLoadStatus.FileNotFound` | "No config file found"  | "Create a config.json at:\n{path}"               |
| `ConfigLoadStatus.ParseError`   | "Invalid config file"   | "JSON parse error:\n{error}"                     |
| Config is null or has 0 items   | "No apps configured"    | "Add items to:\n{path}"                          |

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

In `LoadIconsAsync()`, icons are resolved per item:

1. If `CustomIconPath` is set, use it directly.
2. If `Type == "exe"`, call `CompanionClient.ExtractIconAsync(path)` to extract the EXE's embedded icon via the companion process.
3. If `Type == "url"`, call `CompanionClient.FetchFaviconAsync(path)` to download the site's favicon.
4. If the resolved path is valid, create a `BitmapImage` from the URI.
5. On failure or null, fall back to a default asset:
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
