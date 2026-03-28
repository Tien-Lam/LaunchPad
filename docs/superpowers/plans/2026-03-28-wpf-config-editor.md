# WPF Config Editor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the broken Game Bar file-picker flow with a WPF config editor window hosted in the companion process, providing full CRUD and reorder for launch items.

**Architecture:** The editor is a WPF window inside the existing companion process. The companion's csproj gains `<UseWPF>true</UseWPF>`. On `open-editor` IPC action, the companion spawns an STA thread and shows the WPF window (single instance). The editor reads/writes config.json directly via `ConfigLoader`. A "Save & Refresh" button writes config and sends a `config-updated` notification to the widget via the existing `AppServiceConnection`.

**Tech Stack:** C# / WPF (.NET 8) / Windows App Service IPC / xUnit

**Spec:** `docs/superpowers/specs/2026-03-28-wpf-config-editor-design.md`

---

## File Map

### New files
| File | Responsibility |
|------|---------------|
| `LaunchPad.Companion/Editor/EditorWindow.xaml` | WPF window — dark-themed list-detail layout with toolbar and bottom bar |
| `LaunchPad.Companion/Editor/EditorWindow.xaml.cs` | Code-behind — item selection, add/remove/reorder, save, file picker integration |

### Modified files
| File | Change |
|------|--------|
| `LaunchPad.Companion/LaunchPad.Companion.csproj` | Add `<UseWPF>true</UseWPF>` |
| `LaunchPad.Companion/Program.cs` | Add `open-editor` case, STA thread management, single-instance check, store connection for push notifications, remove `HandleAddExe` |
| `LaunchPad.Widget/LaunchPadWidget.xaml` | Change button to gear glyph, update tooltip and click handler name |
| `LaunchPad.Widget/LaunchPadWidget.xaml.cs` | Rename `OnAddClick` → `OnEditClick`, send `open-editor`, add `ConfigUpdated` event handler |
| `LaunchPad.Widget/Services/CompanionClient.cs` | Replace `AddExeAsync` with `OpenEditorAsync`, add `OnCompanionMessage` handler and `ConfigUpdated` event |
| `LaunchPad.Widget/App.xaml.cs` | Wire `RequestReceived` on companion connection |

---

### Task 1: Enable WPF in companion csproj

**Files:**
- Modify: `LaunchPad.Companion/LaunchPad.Companion.csproj`

- [ ] **Step 1: Add UseWPF to the csproj**

In `LaunchPad.Companion/LaunchPad.Companion.csproj`, add `<UseWPF>true</UseWPF>` to the PropertyGroup:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
    <Platforms>x64;x86;ARM</Platforms>
    <RuntimeIdentifiers>win-x64;win-x86;win-arm64</RuntimeIdentifiers>
    <Nullable>enable</Nullable>
    <UseWindowsForms>true</UseWindowsForms>
    <UseWPF>true</UseWPF>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="System.Drawing.Common" Version="8.0.0" />
    <ProjectReference Include="..\LaunchPad.Shared\LaunchPad.Shared.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Verify it builds**

Run: `dotnet build LaunchPad.Companion/LaunchPad.Companion.csproj`
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add LaunchPad.Companion/LaunchPad.Companion.csproj
git commit -m "feat: enable WPF in companion project"
```

---

### Task 2: Create the WPF EditorWindow

**Files:**
- Create: `LaunchPad.Companion/Editor/EditorWindow.xaml`
- Create: `LaunchPad.Companion/Editor/EditorWindow.xaml.cs`

- [ ] **Step 1: Create the XAML file**

Create `LaunchPad.Companion/Editor/EditorWindow.xaml`:

```xml
<Window x:Class="LaunchPad.Companion.Editor.EditorWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="LaunchPad — Config Editor"
        Width="500" Height="450"
        MinWidth="400" MinHeight="350"
        WindowStartupLocation="CenterScreen"
        Background="#1E1E1E">
    <Window.Resources>
        <Style x:Key="DarkTextBox" TargetType="TextBox">
            <Setter Property="Background" Value="#3C3C3C"/>
            <Setter Property="Foreground" Value="#E0E0E0"/>
            <Setter Property="BorderBrush" Value="#555555"/>
            <Setter Property="BorderThickness" Value="1"/>
            <Setter Property="Padding" Value="5,3"/>
            <Setter Property="FontSize" Value="13"/>
        </Style>
        <Style x:Key="AccentButton" TargetType="Button">
            <Setter Property="Background" Value="#0E639C"/>
            <Setter Property="Foreground" Value="White"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Padding" Value="12,4"/>
            <Setter Property="FontSize" Value="12"/>
            <Setter Property="Cursor" Value="Hand"/>
        </Style>
        <Style x:Key="ToolbarButton" TargetType="Button">
            <Setter Property="Background" Value="#3C3C3C"/>
            <Setter Property="Foreground" Value="#CCCCCC"/>
            <Setter Property="BorderBrush" Value="#555555"/>
            <Setter Property="BorderThickness" Value="1"/>
            <Setter Property="Padding" Value="8,4"/>
            <Setter Property="FontSize" Value="12"/>
            <Setter Property="Cursor" Value="Hand"/>
        </Style>
        <Style x:Key="FieldLabel" TargetType="TextBlock">
            <Setter Property="FontSize" Value="11"/>
            <Setter Property="Foreground" Value="#888888"/>
            <Setter Property="Margin" Value="0,0,0,4"/>
        </Style>
    </Window.Resources>

    <DockPanel>
        <!-- Bottom bar -->
        <Border DockPanel.Dock="Bottom" Background="#252526"
                BorderBrush="#3C3C3C" BorderThickness="0,1,0,0"
                Padding="12,8">
            <DockPanel>
                <TextBlock x:Name="ItemCountLabel" Text="0 items"
                           Foreground="#666666" FontSize="11"
                           VerticalAlignment="Center"/>
                <Button x:Name="SaveButton" Content="Save &amp; Refresh"
                        Style="{StaticResource AccentButton}"
                        HorizontalAlignment="Right" DockPanel.Dock="Right"
                        Click="OnSaveClick"/>
            </DockPanel>
        </Border>

        <!-- Toolbar -->
        <Border DockPanel.Dock="Top" Background="#2D2D2D"
                BorderBrush="#3C3C3C" BorderThickness="0,0,0,1"
                Padding="12,6">
            <StackPanel Orientation="Horizontal">
                <Button Content="+ Add EXE" Style="{StaticResource AccentButton}"
                        Click="OnAddExeClick" Margin="0,0,6,0"/>
                <Button Content="+ Add URL" Style="{StaticResource AccentButton}"
                        Click="OnAddUrlClick" Margin="0,0,6,0"/>
                <Border Width="1" Background="#3C3C3C" Margin="6,0"/>
                <Button x:Name="MoveUpButton" Content="▲"
                        Style="{StaticResource ToolbarButton}"
                        Click="OnMoveUpClick" Margin="6,0,4,0"/>
                <Button x:Name="MoveDownButton" Content="▼"
                        Style="{StaticResource ToolbarButton}"
                        Click="OnMoveDownClick" Margin="0,0,6,0"/>
                <Border Width="1" Background="#3C3C3C" Margin="6,0"/>
                <Button x:Name="RemoveButton" Content="Remove"
                        Background="#5A1D1D" Foreground="#E0E0E0"
                        BorderBrush="#8B3A3A" BorderThickness="1"
                        Padding="12,4" FontSize="12" Cursor="Hand"
                        Click="OnRemoveClick" Margin="6,0,0,0"/>
            </StackPanel>
        </Border>

        <!-- Title bar -->
        <Border DockPanel.Dock="Top" Background="#252526"
                BorderBrush="#3C3C3C" BorderThickness="0,0,0,1"
                Padding="12,8">
            <TextBlock Text="LaunchPad — Config Editor"
                       Foreground="#CCCCCC" FontSize="13"/>
        </Border>

        <!-- Main content: list + edit panel -->
        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="180"/>
                <ColumnDefinition Width="1"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <!-- Left: Item list -->
            <ListBox x:Name="ItemList" Grid.Column="0"
                     Background="#252526" BorderThickness="0"
                     Foreground="#E0E0E0"
                     SelectionChanged="OnItemSelectionChanged"
                     DisplayMemberPath="Name"/>

            <!-- Separator -->
            <Border Grid.Column="1" Background="#3C3C3C"/>

            <!-- Right: Edit panel -->
            <ScrollViewer Grid.Column="2" VerticalScrollBarVisibility="Auto"
                          Background="#1E1E1E">
                <StackPanel x:Name="EditPanel" Margin="16" Visibility="Collapsed">
                    <TextBlock Text="NAME" Style="{StaticResource FieldLabel}"/>
                    <TextBox x:Name="NameBox" Style="{StaticResource DarkTextBox}"
                             Margin="0,0,0,14"/>

                    <TextBlock Text="TYPE" Style="{StaticResource FieldLabel}"/>
                    <TextBlock x:Name="TypeLabel" Foreground="#888888"
                               FontSize="13" Margin="0,0,0,14"
                               Padding="6,5"
                               Background="#3C3C3C"/>

                    <TextBlock Text="PATH" Style="{StaticResource FieldLabel}"/>
                    <DockPanel Margin="0,0,0,14">
                        <Button x:Name="BrowsePathButton" Content="Browse"
                                Style="{StaticResource ToolbarButton}"
                                DockPanel.Dock="Right" Margin="4,0,0,0"
                                Click="OnBrowsePathClick"/>
                        <TextBox x:Name="PathBox" Style="{StaticResource DarkTextBox}"/>
                    </DockPanel>

                    <StackPanel x:Name="ArgsPanel">
                        <TextBlock Text="ARGUMENTS" Style="{StaticResource FieldLabel}"/>
                        <TextBox x:Name="ArgsBox" Style="{StaticResource DarkTextBox}"
                                 Margin="0,0,0,14"/>
                    </StackPanel>

                    <TextBlock Text="CUSTOM ICON" Style="{StaticResource FieldLabel}"/>
                    <DockPanel Margin="0,0,0,14">
                        <Button Content="Browse" Style="{StaticResource ToolbarButton}"
                                DockPanel.Dock="Right" Margin="4,0,0,0"
                                Click="OnBrowseIconClick"/>
                        <TextBox x:Name="IconBox" Style="{StaticResource DarkTextBox}"/>
                    </DockPanel>
                </StackPanel>
            </ScrollViewer>
        </Grid>
    </DockPanel>
</Window>
```

- [ ] **Step 2: Create the code-behind**

Create `LaunchPad.Companion/Editor/EditorWindow.xaml.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using LaunchPad.Shared;

namespace LaunchPad.Companion.Editor;

public partial class EditorWindow : Window
{
    private readonly string _configPath;
    private readonly Action? _onSaved;
    private List<LaunchItemConfig> _items = new();
    private int _previousIndex = -1;

    public EditorWindow(string configPath, Action? onSaved)
    {
        InitializeComponent();
        _configPath = configPath;
        _onSaved = onSaved;
        LoadItems();
    }

    private void LoadItems()
    {
        var result = ConfigLoader.Load(_configPath);
        _items = result.Config?.Items ?? new List<LaunchItemConfig>();
        RefreshList(-1);
    }

    private void RefreshList(int selectIndex)
    {
        ItemList.SelectionChanged -= OnItemSelectionChanged;
        ItemList.Items.Clear();
        foreach (var item in _items)
            ItemList.Items.Add(new ListBoxEntry(item.Name, item.Type.ToString().ToLowerInvariant()));

        if (_items.Count == 0)
        {
            EditPanel.Visibility = Visibility.Collapsed;
            _previousIndex = -1;
        }
        else
        {
            var idx = Math.Clamp(selectIndex, 0, _items.Count - 1);
            ItemList.SelectedIndex = idx;
            _previousIndex = idx;
            ShowItemInForm(idx);
        }
        ItemCountLabel.Text = $"{_items.Count} item{(_items.Count == 1 ? "" : "s")}";
        ItemList.SelectionChanged += OnItemSelectionChanged;
    }

    private void SyncFormToItem()
    {
        var idx = _previousIndex;
        if (idx < 0 || idx >= _items.Count) return;
        var item = _items[idx];
        item.Name = NameBox.Text;
        item.Path = PathBox.Text;
        item.Args = string.IsNullOrWhiteSpace(ArgsBox.Text) ? null : ArgsBox.Text;
        item.Icon = string.IsNullOrWhiteSpace(IconBox.Text) ? null : IconBox.Text;
    }

    private void ShowItemInForm(int index)
    {
        if (index < 0 || index >= _items.Count)
        {
            EditPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var item = _items[index];
        EditPanel.Visibility = Visibility.Visible;
        NameBox.Text = item.Name;
        TypeLabel.Text = item.Type.ToString().ToLowerInvariant();
        PathBox.Text = item.Path;
        ArgsBox.Text = item.Args ?? "";
        IconBox.Text = item.Icon ?? "";

        bool isExe = item.Type == LaunchItemType.Exe;
        ArgsPanel.Visibility = isExe ? Visibility.Visible : Visibility.Collapsed;
        BrowsePathButton.Visibility = isExe ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnItemSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        SyncFormToItem();
        _previousIndex = ItemList.SelectedIndex;
        ShowItemInForm(ItemList.SelectedIndex);
    }

    private void OnAddExeClick(object sender, RoutedEventArgs e)
    {
        var exePath = ExePicker.ShowPickerDialog();
        if (exePath == null) return;

        var displayName = ExePicker.GetDisplayName(exePath);
        var newItem = new LaunchItemConfig
        {
            Name = displayName,
            Type = LaunchItemType.Exe,
            Path = exePath
        };
        SyncFormToItem();
        _items.Add(newItem);
        RefreshList(_items.Count - 1);
    }

    private void OnAddUrlClick(object sender, RoutedEventArgs e)
    {
        var newItem = new LaunchItemConfig
        {
            Name = "New URL",
            Type = LaunchItemType.Url,
            Path = "https://"
        };
        SyncFormToItem();
        _items.Add(newItem);
        RefreshList(_items.Count - 1);
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        var idx = ItemList.SelectedIndex;
        if (idx < 0 || idx >= _items.Count) return;
        _items.RemoveAt(idx);
        RefreshList(idx);
    }

    private void OnMoveUpClick(object sender, RoutedEventArgs e)
    {
        var idx = ItemList.SelectedIndex;
        if (idx <= 0) return;
        SyncFormToItem();
        (_items[idx], _items[idx - 1]) = (_items[idx - 1], _items[idx]);
        RefreshList(idx - 1);
    }

    private void OnMoveDownClick(object sender, RoutedEventArgs e)
    {
        var idx = ItemList.SelectedIndex;
        if (idx < 0 || idx >= _items.Count - 1) return;
        SyncFormToItem();
        (_items[idx], _items[idx + 1]) = (_items[idx + 1], _items[idx]);
        RefreshList(idx + 1);
    }

    private void OnBrowsePathClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select an application",
            Filter = "Executables (*.exe)|*.exe",
            CheckFileExists = true
        };
        if (dialog.ShowDialog() == DialogResult.OK)
            PathBox.Text = dialog.FileName;
    }

    private void OnBrowseIconClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select an icon",
            Filter = "Images (*.png;*.ico;*.jpg;*.bmp)|*.png;*.ico;*.jpg;*.bmp",
            CheckFileExists = true
        };
        if (dialog.ShowDialog() == DialogResult.OK)
            IconBox.Text = dialog.FileName;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        SyncFormToItem();
        var config = new LaunchPadConfig { Items = _items };
        ConfigLoader.Save(_configPath, config);
        _onSaved?.Invoke();
    }
}

internal record ListBoxEntry(string Name, string Type)
{
    public override string ToString() => $"{Name}  [{Type}]";
}
```

- [ ] **Step 3: Verify it builds**

Run: `dotnet build LaunchPad.Companion/LaunchPad.Companion.csproj`
Expected: Build succeeded with 0 errors.

- [ ] **Step 4: Commit**

```bash
git add LaunchPad.Companion/Editor/EditorWindow.xaml LaunchPad.Companion/Editor/EditorWindow.xaml.cs
git commit -m "feat: add WPF config editor window"
```

---

### Task 3: Wire `open-editor` in companion dispatcher

**Files:**
- Modify: `LaunchPad.Companion/Program.cs:1-165`

- [ ] **Step 1: Write a test for editor single-instance logic**

Create `LaunchPad.Tests/EditorManagerTests.cs`:

```csharp
using LaunchPad.Companion;

namespace LaunchPad.Tests;

public class EditorManagerTests
{
    [Fact]
    public void IsEditorOpen_ReturnsFalse_WhenNeverOpened()
    {
        Assert.False(EditorManager.IsEditorOpen);
    }
}
```

Run: `dotnet test LaunchPad.Tests/ --filter EditorManagerTests`
Expected: FAIL — `EditorManager` does not exist.

- [ ] **Step 2: Create EditorManager**

Create `LaunchPad.Companion/Editor/EditorManager.cs`:

```csharp
using System;
using System.Threading;
using System.Windows;

namespace LaunchPad.Companion;

public static class EditorManager
{
    private static Thread? _editorThread;
    private static Window? _editorWindow;
    private static readonly object Lock = new();

    public static bool IsEditorOpen
    {
        get
        {
            lock (Lock)
                return _editorThread != null && _editorThread.IsAlive;
        }
    }

    public static void OpenEditor(string configPath, Action? onSaved)
    {
        lock (Lock)
        {
            if (_editorThread != null && _editorThread.IsAlive)
            {
                _editorWindow?.Dispatcher.Invoke(() => _editorWindow.Activate());
                return;
            }

            _editorThread = new Thread(() =>
            {
                _editorWindow = new Editor.EditorWindow(configPath, onSaved);
                _editorWindow.Closed += (_, _) =>
                {
                    _editorWindow.Dispatcher.InvokeShutdown();
                    lock (Lock)
                    {
                        _editorWindow = null;
                        _editorThread = null;
                    }
                };
                _editorWindow.Show();
                System.Windows.Threading.Dispatcher.Run();
            });
            _editorThread.SetApartmentState(ApartmentState.STA);
            _editorThread.IsBackground = true;
            _editorThread.Start();
        }
    }
}
```

- [ ] **Step 3: Run the test**

Run: `dotnet test LaunchPad.Tests/ --filter EditorManagerTests`
Expected: PASS

- [ ] **Step 4: Add `open-editor` case and remove `add-exe` from Program.cs**

Replace the contents of `LaunchPad.Companion/Program.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using LaunchPad.Shared;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace LaunchPad.Companion;

class Program
{
    private static AppServiceConnection? _connection;
    private static readonly ManualResetEvent ExitEvent = new(false);

    static async Task Main()
    {
        using var mutex = new Mutex(true, "Local\\LaunchPadCompanion", out bool created);
        if (!created)
            return;

        _connection = new AppServiceConnection
        {
            AppServiceName = "com.launchpad.service",
            PackageFamilyName = Package.Current.Id.FamilyName
        };
        _connection.RequestReceived += OnRequestReceived;
        _connection.ServiceClosed += (_, _) => ExitEvent.Set();

        var status = await _connection.OpenAsync();
        if (status != AppServiceConnectionStatus.Success)
            return;

        ExitEvent.WaitOne();
    }

    public static async void NotifyConfigUpdated()
    {
        var connection = _connection;
        if (connection == null) return;
        try
        {
            var message = new ValueSet { ["action"] = "config-updated" };
            await connection.SendMessageAsync(message);
        }
        catch { }
    }

    private static async void OnRequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            var message = args.Request.Message;
            var action = message["action"] as string;

            ValueSet response;
            switch (action)
            {
                case "launch":
                    response = HandleLaunch(message);
                    break;
                case "extract-icon":
                    response = HandleExtractIcon(message);
                    break;
                case "fetch-favicon":
                    response = await HandleFetchFaviconAsync(message);
                    break;
                case "load-config":
                    response = HandleLoadConfig(message);
                    break;
                case "open-editor":
                    response = HandleOpenEditor(message);
                    break;
                default:
                    response = new ValueSet { ["status"] = "error", ["error"] = $"Unknown action: {action}" };
                    break;
            }

            await args.Request.SendResponseAsync(response);
        }
        catch (Exception ex)
        {
            var errorResponse = new ValueSet { ["status"] = "error", ["error"] = ex.Message };
            await args.Request.SendResponseAsync(errorResponse);
        }
        finally
        {
            deferral.Complete();
        }
    }

    private static ValueSet HandleLaunch(ValueSet message)
    {
        var type = message["type"] as string ?? "";
        var path = message["path"] as string ?? "";
        var args = message.ContainsKey("args") ? message["args"] as string : null;

        var (success, error) = LaunchHandler.Launch(type, path, args);

        var response = new ValueSet { ["status"] = success ? "ok" : "error" };
        if (error != null) response["error"] = error;
        return response;
    }

    private static ValueSet HandleExtractIcon(ValueSet message)
    {
        var path = message["path"] as string ?? "";
        var cacheDir = IconExtractor.GetIconCacheDir();
        var (success, iconPath) = IconExtractor.ExtractFromExe(path, cacheDir);

        var response = new ValueSet { ["status"] = success ? "ok" : "error" };
        if (iconPath != null) response["iconPath"] = iconPath;
        return response;
    }

    private static async Task<ValueSet> HandleFetchFaviconAsync(ValueSet message)
    {
        var url = message["url"] as string ?? "";
        var cacheDir = IconExtractor.GetIconCacheDir();
        var (success, iconPath) = await IconExtractor.FetchFaviconAsync(url, cacheDir);

        var response = new ValueSet { ["status"] = success ? "ok" : "error" };
        if (iconPath != null) response["iconPath"] = iconPath;
        return response;
    }

    private static ValueSet HandleLoadConfig(ValueSet message)
    {
        var configPath = message.ContainsKey("configPath")
            ? message["configPath"] as string ?? ConfigLoader.GetDefaultConfigPath()
            : ConfigLoader.GetDefaultConfigPath();

        var result = ConfigLoader.Load(configPath);

        var response = new ValueSet
        {
            ["status"] = result.Status.ToString().ToLowerInvariant(),
            ["configPath"] = configPath
        };

        if (result.Status == ConfigLoadStatus.Success && result.Config != null)
        {
            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = false };
            response["json"] = System.Text.Json.JsonSerializer.Serialize(result.Config, options);
        }

        if (result.ErrorMessage != null)
            response["error"] = result.ErrorMessage;

        return response;
    }

    private static ValueSet HandleOpenEditor(ValueSet message)
    {
        var configPath = message.ContainsKey("configPath")
            ? message["configPath"] as string ?? ConfigLoader.GetDefaultConfigPath()
            : ConfigLoader.GetDefaultConfigPath();

        EditorManager.OpenEditor(configPath, NotifyConfigUpdated);
        return new ValueSet { ["status"] = "ok" };
    }
}
```

- [ ] **Step 5: Verify it builds**

Run: `dotnet build LaunchPad.Companion/LaunchPad.Companion.csproj`
Expected: Build succeeded with 0 errors.

- [ ] **Step 6: Run all tests**

Run: `dotnet test LaunchPad.Tests/`
Expected: All tests pass.

- [ ] **Step 7: Commit**

```bash
git add LaunchPad.Companion/Editor/EditorManager.cs LaunchPad.Companion/Program.cs LaunchPad.Tests/EditorManagerTests.cs
git commit -m "feat: add open-editor IPC action and EditorManager"
```

---

### Task 4: Update widget to send `open-editor` and listen for `config-updated`

**Files:**
- Modify: `LaunchPad.Widget/Services/CompanionClient.cs:1-125`
- Modify: `LaunchPad.Widget/App.xaml.cs:1-82`
- Modify: `LaunchPad.Widget/LaunchPadWidget.xaml:59-69`
- Modify: `LaunchPad.Widget/LaunchPadWidget.xaml.cs:183-198`

- [ ] **Step 1: Update CompanionClient — replace AddExeAsync with OpenEditorAsync, add config-updated handler**

Replace the contents of `LaunchPad.Widget/Services/CompanionClient.cs`:

```csharp
using System;
using System.Text.Json;
using System.Threading.Tasks;
using LaunchPad.Shared;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace LaunchPad.Widget.Services;

public static class CompanionClient
{
    public static event Action? ConfigUpdated;

    public static async Task<(ConfigLoadStatus Status, LaunchPadConfig? Config, string? ConfigPath, string? Error)> LoadConfigAsync()
    {
        var connection = App.CompanionConnection;
        if (connection == null)
            return (ConfigLoadStatus.FileNotFound, null, null, "Companion not connected");

        var request = new ValueSet { ["action"] = "load-config" };
        var response = await connection.SendMessageAsync(request);
        if (response.Status != AppServiceResponseStatus.Success)
            return (ConfigLoadStatus.FileNotFound, null, null, "App Service error");

        var msg = response.Message;
        var status = msg["status"] as string;
        var configPath = msg.ContainsKey("configPath") ? msg["configPath"] as string : null;

        if (status == "success" && msg.ContainsKey("json"))
        {
            var json = msg["json"] as string;
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var config = JsonSerializer.Deserialize<LaunchPadConfig>(json, options);
            return (ConfigLoadStatus.Success, config, configPath, null);
        }

        if (status == "filenotfound")
            return (ConfigLoadStatus.FileNotFound, null, configPath, null);

        var error = msg.ContainsKey("error") ? msg["error"] as string : null;
        return (ConfigLoadStatus.ParseError, null, configPath, error);
    }

    public static async Task<bool> LaunchAsync(string type, string path, string? args = null)
    {
        var connection = App.CompanionConnection;
        if (connection == null) return false;

        var request = new ValueSet
        {
            ["action"] = "launch",
            ["type"] = type,
            ["path"] = path
        };
        if (args != null) request["args"] = args;

        var response = await connection.SendMessageAsync(request);
        if (response.Status != AppServiceResponseStatus.Success) return false;

        return response.Message["status"] as string == "ok";
    }

    public static async Task<string?> ExtractIconAsync(string exePath)
    {
        var connection = App.CompanionConnection;
        if (connection == null) return null;

        var request = new ValueSet
        {
            ["action"] = "extract-icon",
            ["path"] = exePath
        };

        var response = await connection.SendMessageAsync(request);
        if (response.Status != AppServiceResponseStatus.Success) return null;

        if (response.Message["status"] as string == "ok")
            return response.Message["iconPath"] as string;

        return null;
    }

    public static async Task<string?> FetchFaviconAsync(string url)
    {
        var connection = App.CompanionConnection;
        if (connection == null) return null;

        var request = new ValueSet
        {
            ["action"] = "fetch-favicon",
            ["url"] = url
        };

        var response = await connection.SendMessageAsync(request);
        if (response.Status != AppServiceResponseStatus.Success) return null;

        if (response.Message["status"] as string == "ok")
            return response.Message["iconPath"] as string;

        return null;
    }

    public static async Task<bool> OpenEditorAsync()
    {
        var connection = App.CompanionConnection;
        if (connection == null) return false;

        var configPath = ConfigLoader.GetDefaultConfigPath();
        var request = new ValueSet
        {
            ["action"] = "open-editor",
            ["configPath"] = configPath
        };

        var response = await connection.SendMessageAsync(request);
        if (response.Status != AppServiceResponseStatus.Success) return false;

        return response.Message["status"] as string == "ok";
    }

    public static void OnCompanionMessage(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
    {
        var message = args.Request.Message;
        if (message.ContainsKey("action") && message["action"] as string == "config-updated")
        {
            ConfigUpdated?.Invoke();
        }
    }
}
```

- [ ] **Step 2: Wire RequestReceived in App.xaml.cs**

Replace the `OnBackgroundActivated` method in `LaunchPad.Widget/App.xaml.cs`:

```csharp
    protected override void OnBackgroundActivated(BackgroundActivatedEventArgs args)
    {
        base.OnBackgroundActivated(args);

        if (args.TaskInstance.TriggerDetails is AppServiceTriggerDetails details)
        {
            _appServiceDeferral = args.TaskInstance.GetDeferral();
            _companionConnection = details.AppServiceConnection;
            CompanionConnection = _companionConnection;

            _companionConnection.RequestReceived += Services.CompanionClient.OnCompanionMessage;

            args.TaskInstance.Canceled += (_, _) =>
            {
                _appServiceDeferral?.Complete();
                CompanionConnection = null;
            };
        }
    }
```

- [ ] **Step 3: Update the button in LaunchPadWidget.xaml**

Replace the AddButton in `LaunchPad.Widget/LaunchPadWidget.xaml` (lines 59-69):

```xml
        <!-- Edit config button -->
        <Button x:Name="EditButton"
                Content="&#xE713;"
                FontFamily="Segoe MDL2 Assets"
                FontSize="16"
                Width="40" Height="40"
                CornerRadius="20"
                HorizontalAlignment="Right"
                VerticalAlignment="Bottom"
                Margin="0,0,8,8"
                Click="OnEditClick"
                ToolTipService.ToolTip="Edit configuration" />
```

- [ ] **Step 4: Update the click handler in LaunchPadWidget.xaml.cs**

Replace `OnAddClick` (lines 183-198) with:

```csharp
    private async void OnEditClick(object sender, RoutedEventArgs e)
    {
        EditButton.IsEnabled = false;
        try
        {
            await CompanionClient.OpenEditorAsync();
        }
        finally
        {
            EditButton.IsEnabled = true;
        }
    }
```

Add a subscription to `ConfigUpdated` at the end of the `OnLoaded` method (after `await LoadConfigAsync();`):

```csharp
        CompanionClient.ConfigUpdated += async () =>
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                await LoadConfigAsync();
            });
        };
```

- [ ] **Step 5: Build the full solution**

Run: `dotnet build LaunchPad.Shared/LaunchPad.Shared.csproj && dotnet build LaunchPad.Companion/LaunchPad.Companion.csproj`
Expected: Both build with 0 errors.

Note: The widget project (UWP) must be built with MSBuild/VS. The companion and shared projects validate the non-UWP side.

- [ ] **Step 6: Commit**

```bash
git add LaunchPad.Widget/Services/CompanionClient.cs LaunchPad.Widget/App.xaml.cs LaunchPad.Widget/LaunchPadWidget.xaml LaunchPad.Widget/LaunchPadWidget.xaml.cs
git commit -m "feat: wire widget to open-editor and listen for config-updated"
```

---

### Task 5: Update docs

**Files:**
- Modify: `docs/IPC.md`
- Modify: `docs/UI.md`

- [ ] **Step 1: Update IPC.md**

Add the `open-editor` action and `config-updated` notification. Remove the `add-exe` action documentation. Add these sections:

**open-editor** — Widget requests the companion to open the config editor window.

Request:
```
{ "action": "open-editor", "configPath": "<path>" }
```

Response:
```
{ "status": "ok" }
```

If the editor is already open, the companion focuses the existing window.

**config-updated** — Companion notifies the widget that config has been saved from the editor.

Direction: Companion → Widget (unsolicited)
```
{ "action": "config-updated" }
```

Widget handles this by calling `LoadConfigAsync()` to refresh the grid.

- [ ] **Step 2: Update UI.md**

Update the "Add Button" section to reflect the new gear button behavior:
- Content changed from "+" to gear glyph (Segoe MDL2 `\uE713`)
- Tooltip changed to "Edit configuration"
- Click behavior: sends `open-editor` IPC action, companion opens WPF config editor window
- No longer opens a file picker directly

- [ ] **Step 3: Commit**

```bash
git add docs/IPC.md docs/UI.md
git commit -m "docs: update IPC and UI docs for config editor"
```

---

### Task 6: Manual integration test

- [ ] **Step 1: Deploy and test**

1. Open the solution in Visual Studio and deploy (F5)
2. Open Game Bar (Win+G)
3. Open the LaunchPad widget
4. Click the gear button — verify the config editor window appears
5. Click "Add EXE" — verify file picker opens and selected EXE appears in the list
6. Click "Add URL" — verify a blank URL entry appears
7. Edit the name, path, and args fields — verify changes stick when switching items
8. Use up/down buttons to reorder — verify order changes
9. Click "Remove" — verify item is removed
10. Click "Save & Refresh" — verify the widget grid updates to reflect changes
11. Close the editor, click gear again — verify it reopens
12. Click gear while editor is already open — verify it focuses instead of opening a second window
