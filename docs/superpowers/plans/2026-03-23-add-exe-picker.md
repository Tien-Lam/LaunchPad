# Add EXE Picker Implementation Plan

> **For agentic workers:** This project uses **beads** (`bd`) for task tracking. Run `bd prime` for workflow context. Do NOT use TodoWrite/TaskCreate.

**Goal:** Add a "+" button to the widget that opens a file picker via the companion, auto-names the EXE from FileVersionInfo, saves to config, and refreshes the grid.

**Architecture:** Widget sends `add-exe` action via App Service → Companion opens Win32 OpenFileDialog on a STA thread, reads FileVersionInfo for display name, appends to config.json, responds with the new item → Widget reloads config and grid.

**Tech Stack:** Existing stack. `System.Windows.Forms.OpenFileDialog` (already available via System.Drawing.Common dependency chain), `System.Diagnostics.FileVersionInfo`.

---

## File Structure

```
LaunchPad.Shared\ConfigModels.cs          # Add ConfigLoader.Save()
LaunchPad.Companion\ExePicker.cs          # New — file picker + name extraction
LaunchPad.Companion\Program.cs            # Add "add-exe" case to dispatcher
LaunchPad.Tests\ExePickerTests.cs         # New — tests for name extraction
LaunchPad.Widget\Services\CompanionClient.cs  # Add AddExeAsync()
LaunchPad.Widget\LaunchPadWidget.xaml      # Add "+" button
LaunchPad.Widget\LaunchPadWidget.xaml.cs   # Add button click handler
```

---

## Task 1: ConfigLoader.Save

Add a `Save` method to write config back to JSON.

**Files:**
- Modify: `LaunchPad.Shared\ConfigModels.cs`
- Test: `LaunchPad.Tests\ConfigModelsTests.cs`

- [ ] **Step 1: Write failing test**

Add to `V:\Projects\LaunchPad\LaunchPad.Tests\ConfigModelsTests.cs`:

```csharp
[Fact]
public void ConfigLoader_Save_WritesValidJson()
{
    var config = new LaunchPadConfig
    {
        Items = new List<LaunchItemConfig>
        {
            new() { Name = "Test", Type = LaunchItemType.Exe, Path = @"C:\test.exe" }
        }
    };
    var tempFile = Path.GetTempFileName();

    try
    {
        ConfigLoader.Save(tempFile, config);
        var result = ConfigLoader.Load(tempFile);
        Assert.Equal(ConfigLoadStatus.Success, result.Status);
        Assert.Single(result.Config!.Items);
        Assert.Equal("Test", result.Config.Items[0].Name);
    }
    finally
    {
        File.Delete(tempFile);
    }
}

[Fact]
public void ConfigLoader_Save_PreservesAllFields()
{
    var config = new LaunchPadConfig
    {
        Items = new List<LaunchItemConfig>
        {
            new()
            {
                Name = "Discord",
                Type = LaunchItemType.Exe,
                Path = @"C:\Discord\Update.exe",
                Args = "--processStart Discord.exe",
                Icon = @"C:\icons\discord.png"
            }
        }
    };
    var tempFile = Path.GetTempFileName();

    try
    {
        ConfigLoader.Save(tempFile, config);
        var result = ConfigLoader.Load(tempFile);
        Assert.Equal("--processStart Discord.exe", result.Config!.Items[0].Args);
        Assert.Equal(@"C:\icons\discord.png", result.Config.Items[0].Icon);
    }
    finally
    {
        File.Delete(tempFile);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd V:/Projects/LaunchPad
dotnet test LaunchPad.Tests --filter "FullyQualifiedName~ConfigLoader_Save" --no-restore
```

Expected: FAIL — `ConfigLoader.Save` not defined.

- [ ] **Step 3: Implement Save**

Add to `V:\Projects\LaunchPad\LaunchPad.Shared\ConfigModels.cs`, inside the `ConfigLoader` class:

```csharp
public static void Save(string path, LaunchPadConfig config)
{
    var dir = System.IO.Path.GetDirectoryName(path);
    if (dir != null && !Directory.Exists(dir))
        Directory.CreateDirectory(dir);

    var options = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
    var json = JsonSerializer.Serialize(config, options);
    File.WriteAllText(path, json);
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd V:/Projects/LaunchPad
dotnet test LaunchPad.Tests --filter "FullyQualifiedName~ConfigLoader_Save" -v normal
```

Expected: All 2 tests PASS.

- [ ] **Step 5: Commit**

```bash
cd V:/Projects/LaunchPad
git add LaunchPad.Shared/ConfigModels.cs LaunchPad.Tests/ConfigModelsTests.cs
git commit -m "feat: add ConfigLoader.Save for writing config back to JSON"
```

---

## Task 2: ExePicker — Name Extraction and Config Append

Implement the file picker logic and name extraction. The picker itself (OpenFileDialog) can't be unit tested, but the name extraction and config append logic can.

**Files:**
- Create: `LaunchPad.Companion\ExePicker.cs`
- Test: `LaunchPad.Tests\ExePickerTests.cs`

- [ ] **Step 1: Write failing tests**

Write `V:\Projects\LaunchPad\LaunchPad.Tests\ExePickerTests.cs`:

```csharp
using LaunchPad.Companion;
using Xunit;

namespace LaunchPad.Tests;

public class ExePickerTests
{
    [Fact]
    public void GetDisplayName_Notepad_ReturnsFileDescription()
    {
        // notepad.exe has FileDescription "Notepad" on all Windows
        var name = ExePicker.GetDisplayName(@"C:\Windows\notepad.exe");
        Assert.Equal("Notepad", name);
    }

    [Fact]
    public void GetDisplayName_NonexistentExe_ReturnsFileNameWithoutExtension()
    {
        var name = ExePicker.GetDisplayName(@"C:\nonexistent\MyApp.exe");
        Assert.Equal("MyApp", name);
    }

    [Fact]
    public void GetDisplayName_NullPath_ReturnsUnknown()
    {
        var name = ExePicker.GetDisplayName(null);
        Assert.Equal("Unknown", name);
    }

    [Fact]
    public void AppendToConfig_AddsNewItem()
    {
        var config = new LaunchPad.Shared.LaunchPadConfig();
        ExePicker.AppendToConfig(config, @"C:\app.exe", "My App");

        Assert.Single(config.Items);
        Assert.Equal("My App", config.Items[0].Name);
        Assert.Equal(LaunchPad.Shared.LaunchItemType.Exe, config.Items[0].Type);
        Assert.Equal(@"C:\app.exe", config.Items[0].Path);
    }

    [Fact]
    public void AppendToConfig_DoesNotAddDuplicate()
    {
        var config = new LaunchPad.Shared.LaunchPadConfig();
        ExePicker.AppendToConfig(config, @"C:\app.exe", "My App");
        ExePicker.AppendToConfig(config, @"C:\app.exe", "My App");

        Assert.Single(config.Items);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd V:/Projects/LaunchPad
dotnet test LaunchPad.Tests --filter "FullyQualifiedName~ExePickerTests" --no-restore
```

Expected: FAIL — `ExePicker` not defined.

- [ ] **Step 3: Implement ExePicker**

Write `V:\Projects\LaunchPad\LaunchPad.Companion\ExePicker.cs`:

```csharp
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using LaunchPad.Shared;

namespace LaunchPad.Companion;

public static class ExePicker
{
    public static string GetDisplayName(string? exePath)
    {
        if (string.IsNullOrEmpty(exePath))
            return "Unknown";

        try
        {
            if (File.Exists(exePath))
            {
                var info = FileVersionInfo.GetVersionInfo(exePath);
                if (!string.IsNullOrWhiteSpace(info.FileDescription))
                    return info.FileDescription;
            }
        }
        catch { }

        return Path.GetFileNameWithoutExtension(exePath) ?? "Unknown";
    }

    public static void AppendToConfig(LaunchPadConfig config, string exePath, string displayName)
    {
        if (config.Items.Any(i =>
            string.Equals(i.Path, exePath, StringComparison.OrdinalIgnoreCase)))
            return;

        config.Items.Add(new LaunchItemConfig
        {
            Name = displayName,
            Type = LaunchItemType.Exe,
            Path = exePath
        });
    }

    public static string? ShowPickerDialog()
    {
        string? selectedPath = null;

        // OpenFileDialog requires STA thread
        var thread = new Thread(() =>
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Select an application",
                Filter = "Executables (*.exe)|*.exe",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == DialogResult.OK)
                selectedPath = dialog.FileName;
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        return selectedPath;
    }
}
```

- [ ] **Step 4: Add System.Windows.Forms reference to Companion project**

The Companion project needs `System.Windows.Forms` for `OpenFileDialog`. Add `<UseWindowsForms>true</UseWindowsForms>` to the PropertyGroup in `LaunchPad.Companion\LaunchPad.Companion.csproj`.

- [ ] **Step 5: Run tests to verify they pass**

```bash
cd V:/Projects/LaunchPad
dotnet test LaunchPad.Tests --filter "FullyQualifiedName~ExePickerTests" -v normal
```

Expected: All 5 tests PASS.

- [ ] **Step 6: Commit**

```bash
cd V:/Projects/LaunchPad
git add LaunchPad.Companion/ExePicker.cs LaunchPad.Companion/LaunchPad.Companion.csproj LaunchPad.Tests/ExePickerTests.cs
git commit -m "feat: add ExePicker with name extraction and config append"
```

---

## Task 3: Companion Dispatcher — add-exe Action

Wire the `add-exe` action into the companion's request handler.

**Files:**
- Modify: `LaunchPad.Companion\Program.cs`

- [ ] **Step 1: Add the add-exe case**

In `V:\Projects\LaunchPad\LaunchPad.Companion\Program.cs`, add a new case to the `switch (action)` block, after `"fetch-favicon"`:

```csharp
case "add-exe":
    response = HandleAddExe();
    break;
```

Then add the handler method:

```csharp
private static ValueSet HandleAddExe()
{
    var exePath = ExePicker.ShowPickerDialog();
    if (exePath == null)
        return new ValueSet { ["status"] = "cancelled" };

    var displayName = ExePicker.GetDisplayName(exePath);
    var configPath = ConfigLoader.GetDefaultConfigPath();

    var loadResult = ConfigLoader.Load(configPath);
    var config = loadResult.Config ?? new LaunchPadConfig();

    ExePicker.AppendToConfig(config, exePath, displayName);
    ConfigLoader.Save(configPath, config);

    return new ValueSet
    {
        ["status"] = "ok",
        ["name"] = displayName,
        ["path"] = exePath
    };
}
```

Note: `ConfigLoader.GetDefaultConfigPath()` returns the real `%LOCALAPPDATA%` path when called from the companion (not sandboxed). But the widget reads from the sandboxed path. To keep them in sync, the companion must write to the **widget's sandboxed config path**. Update `HandleAddExe` to accept the config path from the widget's message instead:

```csharp
case "add-exe":
    response = HandleAddExe(message);
    break;
```

```csharp
private static ValueSet HandleAddExe(ValueSet message)
{
    var configPath = message["configPath"] as string ?? ConfigLoader.GetDefaultConfigPath();

    var exePath = ExePicker.ShowPickerDialog();
    if (exePath == null)
        return new ValueSet { ["status"] = "cancelled" };

    var displayName = ExePicker.GetDisplayName(exePath);

    var loadResult = ConfigLoader.Load(configPath);
    var config = loadResult.Config ?? new LaunchPadConfig();

    ExePicker.AppendToConfig(config, exePath, displayName);
    ConfigLoader.Save(configPath, config);

    return new ValueSet
    {
        ["status"] = "ok",
        ["name"] = displayName,
        ["path"] = exePath
    };
}
```

- [ ] **Step 2: Verify companion builds**

```bash
cd V:/Projects/LaunchPad
dotnet build LaunchPad.Companion/LaunchPad.Companion.csproj
```

Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
cd V:/Projects/LaunchPad
git add LaunchPad.Companion/Program.cs
git commit -m "feat: add add-exe action to companion dispatcher"
```

---

## Task 4: Widget — AddExe Client Method and UI

Add the client method and the "+" button to the widget.

**Files:**
- Modify: `LaunchPad.Widget\Services\CompanionClient.cs`
- Modify: `LaunchPad.Widget\LaunchPadWidget.xaml`
- Modify: `LaunchPad.Widget\LaunchPadWidget.xaml.cs`

- [ ] **Step 1: Add AddExeAsync to CompanionClient**

Add to `V:\Projects\LaunchPad\LaunchPad.Widget\Services\CompanionClient.cs`:

```csharp
public static async Task<(bool Success, string? Name, string? Path)> AddExeAsync(string configPath)
{
    var connection = App.CompanionConnection;
    if (connection == null) return (false, null, null);

    var request = new ValueSet
    {
        ["action"] = "add-exe",
        ["configPath"] = configPath
    };

    var response = await connection.SendMessageAsync(request);
    if (response.Status != AppServiceResponseStatus.Success) return (false, null, null);

    var status = response.Message["status"] as string;
    if (status == "ok")
    {
        var name = response.Message["name"] as string;
        var path = response.Message["path"] as string;
        return (true, name, path);
    }

    return (false, null, null);
}
```

- [ ] **Step 2: Add "+" button to XAML**

In `V:\Projects\LaunchPad\LaunchPad.Widget\LaunchPadWidget.xaml`, add a Button after the `GridView` closing tag and before the `EmptyState` StackPanel:

```xml
<!-- Add app button -->
<Button x:Name="AddButton"
        Content="+"
        FontSize="20" FontWeight="Bold"
        Width="40" Height="40"
        CornerRadius="20"
        HorizontalAlignment="Right"
        VerticalAlignment="Bottom"
        Margin="0,0,8,8"
        Click="OnAddClick"
        ToolTipService.ToolTip="Add application" />
```

- [ ] **Step 3: Add click handler to code-behind**

Add to `V:\Projects\LaunchPad\LaunchPad.Widget\LaunchPadWidget.xaml.cs`:

```csharp
private async void OnAddClick(object sender, RoutedEventArgs e)
{
    AddButton.IsEnabled = false;
    try
    {
        var configPath = ConfigLoader.GetDefaultConfigPath();
        var (success, name, path) = await CompanionClient.AddExeAsync(configPath);

        if (success)
            await LoadConfigAsync();
    }
    finally
    {
        AddButton.IsEnabled = true;
    }
}
```

- [ ] **Step 4: Build full solution**

```bash
cd V:/Projects/LaunchPad
msbuild LaunchPad.sln /p:Configuration=Debug /p:Platform=x64
```

Expected: 0 errors, 0 warnings.

- [ ] **Step 5: Run all tests**

```bash
cd V:/Projects/LaunchPad
dotnet test LaunchPad.Tests -v normal
```

Expected: All tests pass (17 existing + 2 Save + 5 ExePicker = 24 total).

- [ ] **Step 6: Commit**

```bash
cd V:/Projects/LaunchPad
git add LaunchPad.Widget/Services/CompanionClient.cs LaunchPad.Widget/LaunchPadWidget.xaml LaunchPad.Widget/LaunchPadWidget.xaml.cs
git commit -m "feat: add '+' button to widget for adding EXEs via file picker"
```

---

## Task 5: Deploy and Test

- [ ] **Step 1: Build and deploy**

```bash
cd V:/Projects/LaunchPad
msbuild LaunchPad.sln /p:Configuration=Debug /p:Platform=x64
powershell -Command "Add-AppxPackage -ForceApplicationShutdown -Register 'V:\Projects\LaunchPad\LaunchPad.Package\bin\x64\Debug\AppxManifest.xml'"
```

- [ ] **Step 2: Manual test**

1. Open Game Bar (Win+G)
2. Open LaunchPad widget
3. Click the "+" button
4. File picker should appear — select an EXE (e.g., notepad.exe)
5. Widget grid should refresh with the new item
6. Close and reopen widget — item should persist
