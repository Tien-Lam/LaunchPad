# LaunchPad Game Bar Widget Implementation Plan

> **For agentic workers:** This project uses **beads** (`bd`) for task tracking. Run `bd prime` for workflow context, `bd ready` for available work. Do NOT use TodoWrite/TaskCreate — use beads.

**Goal:** Build an Xbox Game Bar widget that launches apps (EXEs, URLs, Store apps) from a configurable grid overlay.

**Architecture:** UWP XAML widget + Win32 companion process in a single MSIX package. The widget renders a 4-column grid and communicates with the companion via App Service IPC. The companion handles launching EXEs (outside UWP sandbox) and extracting icons.

**Tech Stack:** C#, UWP XAML, .NET 8 (companion), .NET Standard 2.0 (shared), Microsoft.Gaming.XboxGameBar NuGet, Windows Application Packaging Project (WAPPROJ)

**Spec:** `docs/2026-03-23-launchpad-gamebar-widget-design.md`

## Beads Issue Map

| Task | Beads ID | Depends On |
|------|----------|------------|
| 1. Solution Scaffolding | `LaunchPad-tr8` | — |
| 2. Shared Config Model & Loader | `LaunchPad-jbj` | Task 1 |
| 3. Companion Launch Handler | `LaunchPad-4zo` | Task 1 |
| 4. Companion Icon Extractor | `LaunchPad-w5a` | Task 1 |
| 5. Companion App Service Host | `LaunchPad-stc` | Tasks 3, 4 |
| 6. Package Manifest | `LaunchPad-8qy` | Task 1 |
| 7. Widget App Activation | `LaunchPad-e7f` | Task 6 |
| 8. Widget CompanionClient | `LaunchPad-57g` | Task 7 |
| 9. Widget Grid UI | `LaunchPad-vjq` | Tasks 2, 8 |
| 10. Integration & Verification | `LaunchPad-23g` | Tasks 5, 9 |

Epic: `LaunchPad-037`

---

## File Structure

```
V:\Projects\LaunchPad\
├── LaunchPad.sln
├── LaunchPad.Widget\                    # UWP App (C#)
│   ├── LaunchPad.Widget.csproj
│   ├── App.xaml
│   ├── App.xaml.cs
│   ├── LaunchPadWidget.xaml
│   ├── LaunchPadWidget.xaml.cs
│   ├── Models\LaunchItem.cs
│   ├── Services\CompanionClient.cs
│   ├── Assets\                          # Default icon fallbacks
│   │   ├── DefaultApp.png
│   │   └── DefaultGlobe.png
│   ├── GameBar\Widget.png               # Game Bar widget list icon
│   └── Properties\AssemblyInfo.cs
├── LaunchPad.Companion\                 # .NET 8 Win32 Console App
│   ├── LaunchPad.Companion.csproj
│   ├── Program.cs
│   ├── LaunchHandler.cs
│   └── IconExtractor.cs
├── LaunchPad.Shared\                    # .NET Standard 2.0 Class Library
│   ├── LaunchPad.Shared.csproj
│   └── ConfigModels.cs
├── LaunchPad.Tests\                     # xUnit Test Project
│   ├── LaunchPad.Tests.csproj
│   ├── ConfigModelsTests.cs
│   ├── LaunchHandlerTests.cs
│   └── IconExtractorTests.cs
└── LaunchPad.Package\                   # Windows Application Packaging Project
    ├── LaunchPad.Package.wapproj
    ├── Package.appxmanifest
    └── Images\                          # App tile/splash assets
```

---

## Task 1: Solution Scaffolding

Create the solution, all projects, and wire them together with references and NuGet packages.

**Files:**
- Create: `V:\Projects\LaunchPad\LaunchPad.sln`
- Create: `V:\Projects\LaunchPad\LaunchPad.Shared\LaunchPad.Shared.csproj`
- Create: `V:\Projects\LaunchPad\LaunchPad.Companion\LaunchPad.Companion.csproj`
- Create: `V:\Projects\LaunchPad\LaunchPad.Tests\LaunchPad.Tests.csproj`
- Create: `V:\Projects\LaunchPad\LaunchPad.Widget\LaunchPad.Widget.csproj`
- Create: `V:\Projects\LaunchPad\LaunchPad.Package\LaunchPad.Package.wapproj`

- [ ] **Step 1: Create project directories**

```bash
mkdir -p V:/Projects/LaunchPad/{LaunchPad.Widget/{Models,Services,Assets,GameBar,Properties},LaunchPad.Companion,LaunchPad.Shared,LaunchPad.Tests,LaunchPad.Package/Images}
```

- [ ] **Step 2: Create the Shared class library**

```bash
cd V:/Projects/LaunchPad/LaunchPad.Shared
dotnet new classlib -n LaunchPad.Shared --framework netstandard2.0 --force
```

Then add System.Text.Json:

```bash
cd V:/Projects/LaunchPad/LaunchPad.Shared
dotnet add package System.Text.Json --version 8.0.5
```

Delete the auto-generated `Class1.cs`:

```bash
rm V:/Projects/LaunchPad/LaunchPad.Shared/Class1.cs
```

- [ ] **Step 3: Create the Companion console app**

```bash
cd V:/Projects/LaunchPad/LaunchPad.Companion
dotnet new console -n LaunchPad.Companion --framework net8.0 --force
```

Update `LaunchPad.Companion.csproj` to target Windows with WinRT support and add dependencies:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>false</SelfContained>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="System.Drawing.Common" Version="8.0.0" />
    <ProjectReference Include="..\LaunchPad.Shared\LaunchPad.Shared.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Create the Tests project**

```bash
cd V:/Projects/LaunchPad/LaunchPad.Tests
dotnet new xunit -n LaunchPad.Tests --force
```

Add project references:

```bash
cd V:/Projects/LaunchPad/LaunchPad.Tests
dotnet add reference ../LaunchPad.Shared/LaunchPad.Shared.csproj
dotnet add reference ../LaunchPad.Companion/LaunchPad.Companion.csproj
```

- [ ] **Step 5: Create the UWP Widget project file**

Write `V:\Projects\LaunchPad\LaunchPad.Widget\LaunchPad.Widget.csproj`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" DefaultTargets="Build"
         xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props"
          Condition="Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')" />
  <PropertyGroup>
    <Configuration Condition=" '$(Configuration)' == '' ">Debug</Configuration>
    <Platform Condition=" '$(Platform)' == '' ">x64</Platform>
    <ProjectGuid>{B1A2C3D4-E5F6-7890-ABCD-111111111111}</ProjectGuid>
    <OutputType>AppContainerExe</OutputType>
    <AppDesignFolder>Properties</AppDesignFolder>
    <RootNamespace>LaunchPad.Widget</RootNamespace>
    <AssemblyName>LaunchPad.Widget</AssemblyName>
    <DefaultLanguage>en-US</DefaultLanguage>
    <TargetPlatformIdentifier>UAP</TargetPlatformIdentifier>
    <TargetPlatformVersion Condition=" '$(TargetPlatformVersion)' == '' ">10.0.19041.0</TargetPlatformVersion>
    <TargetPlatformMinVersion>10.0.19041.0</TargetPlatformMinVersion>
    <MinimumVisualStudioVersion>14</MinimumVisualStudioVersion>
    <FileAlignment>512</FileAlignment>
    <ProjectTypeGuids>{A5A43C5B-DE2A-4C0C-9213-0A381AF9435A};{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}</ProjectTypeGuids>
    <WindowsXamlEnableOverview>true</WindowsXamlEnableOverview>
  </PropertyGroup>

  <PropertyGroup Condition="'$(Configuration)|$(Platform)' == 'Debug|x64'">
    <DebugSymbols>true</DebugSymbols>
    <OutputPath>bin\x64\Debug\</OutputPath>
    <DefineConstants>DEBUG;TRACE;NETFX_CORE;WINDOWS_UWP</DefineConstants>
    <DebugType>full</DebugType>
    <PlatformTarget>x64</PlatformTarget>
    <UseVSHostingProcess>false</UseVSHostingProcess>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)|$(Platform)' == 'Release|x64'">
    <OutputPath>bin\x64\Release\</OutputPath>
    <DefineConstants>TRACE;NETFX_CORE;WINDOWS_UWP</DefineConstants>
    <Optimize>true</Optimize>
    <DebugType>pdbonly</DebugType>
    <PlatformTarget>x64</PlatformTarget>
    <UseVSHostingProcess>false</UseVSHostingProcess>
    <UseDotNetNativeToolchain>true</UseDotNetNativeToolchain>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="App.xaml.cs">
      <DependentUpon>App.xaml</DependentUpon>
    </Compile>
    <Compile Include="LaunchPadWidget.xaml.cs">
      <DependentUpon>LaunchPadWidget.xaml</DependentUpon>
    </Compile>
    <Compile Include="Models\LaunchItem.cs" />
    <Compile Include="Services\CompanionClient.cs" />
    <Compile Include="Properties\AssemblyInfo.cs" />
  </ItemGroup>

  <ItemGroup>
    <ApplicationDefinition Include="App.xaml">
      <Generator>MSBuild:Compile</Generator>
      <SubType>Designer</SubType>
    </ApplicationDefinition>
    <Page Include="LaunchPadWidget.xaml">
      <Generator>MSBuild:Compile</Generator>
      <SubType>Designer</SubType>
    </Page>
  </ItemGroup>

  <ItemGroup>
    <Content Include="Assets\DefaultApp.png" />
    <Content Include="Assets\DefaultGlobe.png" />
    <Content Include="GameBar\Widget.png" />
  </ItemGroup>

  <ItemGroup>
    <AppxManifest Include="Package.appxmanifest">
      <SubType>Designer</SubType>
    </AppxManifest>
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NETCore.UniversalWindowsPlatform">
      <Version>6.2.14</Version>
    </PackageReference>
    <PackageReference Include="Microsoft.Gaming.XboxGameBar">
      <Version>5.6.230401001</Version>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\LaunchPad.Shared\LaunchPad.Shared.csproj" />
  </ItemGroup>

  <PropertyGroup Condition=" '$(VisualStudioVersion)' == '' or '$(VisualStudioVersion)' &lt; '14.0' ">
    <VisualStudioVersion>14.0</VisualStudioVersion>
  </PropertyGroup>
  <Import Project="$(MSBuildExtensionsPath)\Microsoft\WindowsXaml\v$(VisualStudioVersion)\Microsoft.Windows.UI.Xaml.CSharp.targets" />
</Project>
```

- [ ] **Step 6: Create the Packaging project**

Write `V:\Projects\LaunchPad\LaunchPad.Package\LaunchPad.Package.wapproj`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" DefaultTargets="Build"
         xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup Label="Globals">
    <ProjectGuid>{B1A2C3D4-E5F6-7890-ABCD-222222222222}</ProjectGuid>
    <TargetPlatformVersion>10.0.19041.0</TargetPlatformVersion>
    <TargetPlatformMinVersion>10.0.19041.0</TargetPlatformMinVersion>
    <DefaultLanguage>en-US</DefaultLanguage>
    <AppxPackageSigningEnabled>false</AppxPackageSigningEnabled>
    <EntryPointProjectUniqueName>..\LaunchPad.Widget\LaunchPad.Widget.csproj</EntryPointProjectUniqueName>
  </PropertyGroup>
  <Import Project="$(WapProjPath)\Microsoft.DesktopBridge.props" />
  <ItemGroup>
    <ProjectReference Include="..\LaunchPad.Widget\LaunchPad.Widget.csproj" />
    <ProjectReference Include="..\LaunchPad.Companion\LaunchPad.Companion.csproj" />
  </ItemGroup>
  <ItemGroup>
    <AppxManifest Include="Package.appxmanifest">
      <SubType>Designer</SubType>
    </AppxManifest>
  </ItemGroup>
  <Import Project="$(WapProjPath)\Microsoft.DesktopBridge.targets" />
</Project>
```

- [ ] **Step 7: Create UWP AssemblyInfo.cs**

Write `V:\Projects\LaunchPad\LaunchPad.Widget\Properties\AssemblyInfo.cs`:

```csharp
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("LaunchPad.Widget")]
[assembly: AssemblyProduct("LaunchPad")]
[assembly: ComVisible(false)]
```

- [ ] **Step 8: Create placeholder asset files**

Create 48x48 PNG placeholder images for:
- `V:\Projects\LaunchPad\LaunchPad.Widget\Assets\DefaultApp.png` (generic app icon)
- `V:\Projects\LaunchPad\LaunchPad.Widget\Assets\DefaultGlobe.png` (generic globe icon for URLs)
- `V:\Projects\LaunchPad\LaunchPad.Widget\GameBar\Widget.png` (Game Bar widget list icon)

These can be simple solid-color squares for now. Replace with proper icons later.

- [ ] **Step 9: Create the solution file and add projects**

```bash
cd V:/Projects/LaunchPad
dotnet new sln -n LaunchPad
dotnet sln add LaunchPad.Shared/LaunchPad.Shared.csproj
dotnet sln add LaunchPad.Companion/LaunchPad.Companion.csproj
dotnet sln add LaunchPad.Tests/LaunchPad.Tests.csproj
```

The UWP widget and WAPPROJ use old-style project format, so add them manually to the .sln. Open `LaunchPad.sln` and append these project entries (use exact GUIDs from the .csproj/.wapproj files):

```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "LaunchPad.Widget", "LaunchPad.Widget\LaunchPad.Widget.csproj", "{B1A2C3D4-E5F6-7890-ABCD-111111111111}"
EndProject
Project("{C7167F0D-BC9F-4E6E-AFE1-012C56B48DB5}") = "LaunchPad.Package", "LaunchPad.Package\LaunchPad.Package.wapproj", "{B1A2C3D4-E5F6-7890-ABCD-222222222222}"
EndProject
```

- [ ] **Step 10: Verify non-UWP projects build**

```bash
cd V:/Projects/LaunchPad
dotnet build LaunchPad.Shared/LaunchPad.Shared.csproj
dotnet build LaunchPad.Companion/LaunchPad.Companion.csproj
dotnet build LaunchPad.Tests/LaunchPad.Tests.csproj
```

Expected: All three build successfully.

Note: The UWP widget and packaging project require MSBuild (Visual Studio) to build. They will be verified later via `msbuild` or opening in VS.

- [ ] **Step 11: Commit**

```bash
cd V:/Projects/LaunchPad
git init
echo "bin/\nobj/\n.vs/\n*.user\nAppPackages/\n.superpowers/" > .gitignore
git add .
git commit -m "chore: scaffold LaunchPad solution with all projects"
```

---

## Task 2: Shared Config Model & Loader

Define the JSON config types and a loader with deserialization. Test-driven.

**Files:**
- Create: `LaunchPad.Shared\ConfigModels.cs`
- Test: `LaunchPad.Tests\ConfigModelsTests.cs`

- [ ] **Step 1: Write failing tests for config deserialization**

Write `V:\Projects\LaunchPad\LaunchPad.Tests\ConfigModelsTests.cs`:

```csharp
using System.IO;
using System.Text.Json;
using LaunchPad.Shared;
using Xunit;

namespace LaunchPad.Tests;

public class ConfigModelsTests
{
    [Fact]
    public void Deserialize_ValidConfig_ReturnsItems()
    {
        var json = """
        {
          "items": [
            { "name": "Notepad", "type": "exe", "path": "C:\\Windows\\notepad.exe" },
            { "name": "Google", "type": "url", "path": "https://google.com" }
          ]
        }
        """;

        var config = JsonSerializer.Deserialize<LaunchPadConfig>(json);

        Assert.NotNull(config);
        Assert.Equal(2, config!.Items.Count);
        Assert.Equal("Notepad", config.Items[0].Name);
        Assert.Equal(LaunchItemType.Exe, config.Items[0].Type);
        Assert.Equal("C:\\Windows\\notepad.exe", config.Items[0].Path);
        Assert.Null(config.Items[0].Args);
        Assert.Null(config.Items[0].Icon);
    }

    [Fact]
    public void Deserialize_WithOptionalFields_ParsesCorrectly()
    {
        var json = """
        {
          "items": [
            {
              "name": "Discord",
              "type": "exe",
              "path": "C:\\Discord\\Update.exe",
              "args": "--processStart Discord.exe",
              "icon": "C:\\icons\\discord.png"
            }
          ]
        }
        """;

        var config = JsonSerializer.Deserialize<LaunchPadConfig>(json);

        Assert.Equal("--processStart Discord.exe", config!.Items[0].Args);
        Assert.Equal("C:\\icons\\discord.png", config.Items[0].Icon);
    }

    [Fact]
    public void Deserialize_EmptyItems_ReturnsEmptyList()
    {
        var json = """{ "items": [] }""";

        var config = JsonSerializer.Deserialize<LaunchPadConfig>(json);

        Assert.NotNull(config);
        Assert.Empty(config!.Items);
    }

    [Fact]
    public void Deserialize_AllTypes_ParsesCorrectly()
    {
        var json = """
        {
          "items": [
            { "name": "App", "type": "exe", "path": "app.exe" },
            { "name": "Site", "type": "url", "path": "https://example.com" },
            { "name": "Store", "type": "store", "path": "spotify:" }
          ]
        }
        """;

        var config = JsonSerializer.Deserialize<LaunchPadConfig>(json);

        Assert.Equal(LaunchItemType.Exe, config!.Items[0].Type);
        Assert.Equal(LaunchItemType.Url, config!.Items[1].Type);
        Assert.Equal(LaunchItemType.Store, config!.Items[2].Type);
    }

    [Fact]
    public void ConfigLoader_MissingFile_ReturnsFileNotFound()
    {
        var result = ConfigLoader.Load("C:\\nonexistent\\path\\config.json");
        Assert.Equal(ConfigLoadStatus.FileNotFound, result.Status);
        Assert.Null(result.Config);
    }

    [Fact]
    public void ConfigLoader_ValidFile_ReturnsSuccess()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, """{ "items": [{ "name": "Test", "type": "exe", "path": "test.exe" }] }""");

        try
        {
            var result = ConfigLoader.Load(tempFile);
            Assert.Equal(ConfigLoadStatus.Success, result.Status);
            Assert.NotNull(result.Config);
            Assert.Single(result.Config!.Items);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ConfigLoader_MalformedJson_ReturnsParseError()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "{ not valid json }}}");

        try
        {
            var result = ConfigLoader.Load(tempFile);
            Assert.Equal(ConfigLoadStatus.ParseError, result.Status);
            Assert.NotNull(result.ErrorMessage);
            Assert.Null(result.Config);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd V:/Projects/LaunchPad
dotnet test LaunchPad.Tests --filter "FullyQualifiedName~ConfigModelsTests" --no-restore
```

Expected: FAIL — `LaunchPadConfig`, `LaunchItemType`, `ConfigLoader` not defined.

- [ ] **Step 3: Implement ConfigModels**

Write `V:\Projects\LaunchPad\LaunchPad.Shared\ConfigModels.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LaunchPad.Shared;

public class LaunchPadConfig
{
    [JsonPropertyName("items")]
    public List<LaunchItemConfig> Items { get; set; } = new();
}

public class LaunchItemConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LaunchItemType Type { get; set; }

    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("args")]
    public string? Args { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LaunchItemType
{
    Exe,
    Url,
    Store
}

public class ConfigLoadResult
{
    public LaunchPadConfig? Config { get; set; }
    public ConfigLoadStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum ConfigLoadStatus
{
    Success,
    FileNotFound,
    ParseError
}

public static class ConfigLoader
{
    public static ConfigLoadResult Load(string path)
    {
        if (!File.Exists(path))
            return new ConfigLoadResult { Status = ConfigLoadStatus.FileNotFound };

        try
        {
            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var config = JsonSerializer.Deserialize<LaunchPadConfig>(json, options);
            return new ConfigLoadResult { Config = config, Status = ConfigLoadStatus.Success };
        }
        catch (JsonException ex)
        {
            return new ConfigLoadResult { Status = ConfigLoadStatus.ParseError, ErrorMessage = ex.Message };
        }
    }

    public static string GetDefaultConfigPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return System.IO.Path.Combine(localAppData, "LaunchPad", "config.json");
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd V:/Projects/LaunchPad
dotnet test LaunchPad.Tests --filter "FullyQualifiedName~ConfigModelsTests" -v normal
```

Expected: All 7 tests PASS.

- [ ] **Step 5: Commit**

```bash
cd V:/Projects/LaunchPad
git add LaunchPad.Shared/ConfigModels.cs LaunchPad.Tests/ConfigModelsTests.cs
git commit -m "feat: add config model and loader with JSON deserialization"
```

---

## Task 3: Companion — Launch Handler

Implement the logic for launching EXEs, URLs, and Store/protocol apps. Test-driven.

**Files:**
- Create: `LaunchPad.Companion\LaunchHandler.cs`
- Test: `LaunchPad.Tests\LaunchHandlerTests.cs`

- [ ] **Step 1: Write failing tests for LaunchHandler**

Write `V:\Projects\LaunchPad\LaunchPad.Tests\LaunchHandlerTests.cs`:

```csharp
using LaunchPad.Companion;
using Xunit;

namespace LaunchPad.Tests;

public class LaunchHandlerTests
{
    [Fact]
    public void BuildProcessStartInfo_Exe_SetsFileName()
    {
        var info = LaunchHandler.BuildProcessStartInfo("exe", @"C:\Windows\notepad.exe", null);

        Assert.Equal(@"C:\Windows\notepad.exe", info.FileName);
        Assert.Equal("", info.Arguments);
        Assert.True(info.UseShellExecute);
    }

    [Fact]
    public void BuildProcessStartInfo_ExeWithArgs_SetsArguments()
    {
        var info = LaunchHandler.BuildProcessStartInfo("exe", @"C:\app.exe", "--verbose --port 8080");

        Assert.Equal(@"C:\app.exe", info.FileName);
        Assert.Equal("--verbose --port 8080", info.Arguments);
    }

    [Fact]
    public void BuildProcessStartInfo_Url_SetsFileNameToUrl()
    {
        var info = LaunchHandler.BuildProcessStartInfo("url", "https://youtube.com", null);

        Assert.Equal("https://youtube.com", info.FileName);
        Assert.True(info.UseShellExecute);
    }

    [Fact]
    public void BuildProcessStartInfo_Store_SetsFileNameToProtocol()
    {
        var info = LaunchHandler.BuildProcessStartInfo("store", "spotify:", null);

        Assert.Equal("spotify:", info.FileName);
        Assert.True(info.UseShellExecute);
    }

    [Fact]
    public void BuildProcessStartInfo_UnknownType_ThrowsArgumentException()
    {
        Assert.Throws<System.ArgumentException>(
            () => LaunchHandler.BuildProcessStartInfo("unknown", "foo", null));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd V:/Projects/LaunchPad
dotnet test LaunchPad.Tests --filter "FullyQualifiedName~LaunchHandlerTests" --no-restore
```

Expected: FAIL — `LaunchHandler` not defined.

- [ ] **Step 3: Implement LaunchHandler**

Write `V:\Projects\LaunchPad\LaunchPad.Companion\LaunchHandler.cs`:

```csharp
using System;
using System.Diagnostics;

namespace LaunchPad.Companion;

public static class LaunchHandler
{
    public static ProcessStartInfo BuildProcessStartInfo(string type, string path, string? args)
    {
        return type.ToLowerInvariant() switch
        {
            "exe" => new ProcessStartInfo
            {
                FileName = path,
                Arguments = args ?? "",
                UseShellExecute = true
            },
            "url" or "store" => new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            },
            _ => throw new ArgumentException($"Unknown launch type: {type}", nameof(type))
        };
    }

    public static (bool Success, string? Error) Launch(string type, string path, string? args)
    {
        try
        {
            var startInfo = BuildProcessStartInfo(type, path, args);
            Process.Start(startInfo);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd V:/Projects/LaunchPad
dotnet test LaunchPad.Tests --filter "FullyQualifiedName~LaunchHandlerTests" -v normal
```

Expected: All 5 tests PASS.

- [ ] **Step 5: Commit**

```bash
cd V:/Projects/LaunchPad
git add LaunchPad.Companion/LaunchHandler.cs LaunchPad.Tests/LaunchHandlerTests.cs
git commit -m "feat: add LaunchHandler for exe/url/store process launching"
```

---

## Task 4: Companion — Icon Extractor

Extract icons from EXE files and fetch favicons for URLs. Test-driven for EXE extraction.

**Files:**
- Create: `LaunchPad.Companion\IconExtractor.cs`
- Test: `LaunchPad.Tests\IconExtractorTests.cs`

- [ ] **Step 1: Write failing tests for IconExtractor**

Write `V:\Projects\LaunchPad\LaunchPad.Tests\IconExtractorTests.cs`:

```csharp
using System.IO;
using LaunchPad.Companion;
using Xunit;

namespace LaunchPad.Tests;

public class IconExtractorTests
{
    [Fact]
    public void GetCacheFileName_ReturnsDeterministicHash()
    {
        var name1 = IconExtractor.GetCacheFileName(@"C:\Windows\notepad.exe");
        var name2 = IconExtractor.GetCacheFileName(@"C:\Windows\notepad.exe");

        Assert.Equal(name1, name2);
        Assert.EndsWith(".png", name1);
    }

    [Fact]
    public void GetCacheFileName_DifferentPaths_DifferentNames()
    {
        var name1 = IconExtractor.GetCacheFileName(@"C:\app1.exe");
        var name2 = IconExtractor.GetCacheFileName(@"C:\app2.exe");

        Assert.NotEqual(name1, name2);
    }

    [Fact]
    public void ExtractFromExe_ValidExe_SavesPng()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), "launchpad-test-icons");
        Directory.CreateDirectory(cacheDir);

        try
        {
            // notepad.exe exists on all Windows installations
            var result = IconExtractor.ExtractFromExe(@"C:\Windows\notepad.exe", cacheDir);

            Assert.True(result.Success);
            Assert.NotNull(result.IconPath);
            Assert.True(File.Exists(result.IconPath));
        }
        finally
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, true);
        }
    }

    [Fact]
    public void ExtractFromExe_NonexistentExe_ReturnsFailure()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), "launchpad-test-icons");
        Directory.CreateDirectory(cacheDir);

        try
        {
            var result = IconExtractor.ExtractFromExe(@"C:\nonexistent\app.exe", cacheDir);

            Assert.False(result.Success);
        }
        finally
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, true);
        }
    }

    [Fact]
    public void GetFaviconUrl_ExtractsDomain()
    {
        var url = IconExtractor.GetFaviconUrl("https://www.youtube.com/watch?v=123");

        Assert.Contains("youtube.com", url);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd V:/Projects/LaunchPad
dotnet test LaunchPad.Tests --filter "FullyQualifiedName~IconExtractorTests" --no-restore
```

Expected: FAIL — `IconExtractor` not defined.

- [ ] **Step 3: Implement IconExtractor**

Write `V:\Projects\LaunchPad\LaunchPad.Companion\IconExtractor.cs`:

```csharp
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace LaunchPad.Companion;

public static class IconExtractor
{
    private static readonly HttpClient HttpClient = new();

    public static string GetCacheFileName(string inputPath)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(inputPath));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant() + ".png";
    }

    public static string GetIconCacheDir()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(localAppData, "LaunchPad", "icons");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static (bool Success, string? IconPath) ExtractFromExe(string exePath, string cacheDir)
    {
        try
        {
            if (!File.Exists(exePath))
                return (false, null);

            var cacheFile = Path.Combine(cacheDir, GetCacheFileName(exePath));

            // Check if cached icon is still valid
            if (File.Exists(cacheFile))
            {
                var cacheTime = File.GetLastWriteTimeUtc(cacheFile);
                var exeTime = File.GetLastWriteTimeUtc(exePath);
                if (cacheTime >= exeTime)
                    return (true, cacheFile);
            }

            using var icon = Icon.ExtractAssociatedIcon(exePath);
            if (icon == null)
                return (false, null);

            using var bitmap = icon.ToBitmap();
            bitmap.Save(cacheFile, ImageFormat.Png);
            return (true, cacheFile);
        }
        catch (Exception)
        {
            return (false, null);
        }
    }

    public static string GetFaviconUrl(string url)
    {
        var uri = new Uri(url);
        return $"https://www.google.com/s2/favicons?domain={uri.Host}&sz=64";
    }

    public static async Task<(bool Success, string? IconPath)> FetchFaviconAsync(string url, string cacheDir)
    {
        try
        {
            var cacheFile = Path.Combine(cacheDir, GetCacheFileName(url));
            if (File.Exists(cacheFile))
                return (true, cacheFile);

            var faviconUrl = GetFaviconUrl(url);
            var bytes = await HttpClient.GetByteArrayAsync(faviconUrl);
            await File.WriteAllBytesAsync(cacheFile, bytes);
            return (true, cacheFile);
        }
        catch (Exception)
        {
            return (false, null);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd V:/Projects/LaunchPad
dotnet test LaunchPad.Tests --filter "FullyQualifiedName~IconExtractorTests" -v normal
```

Expected: All 5 tests PASS.

- [ ] **Step 5: Commit**

```bash
cd V:/Projects/LaunchPad
git add LaunchPad.Companion/IconExtractor.cs LaunchPad.Tests/IconExtractorTests.cs
git commit -m "feat: add IconExtractor for EXE icon extraction and favicon fetching"
```

---

## Task 5: Companion — App Service Host

Wire up the companion's entry point to host an App Service and dispatch incoming requests to LaunchHandler and IconExtractor.

**Files:**
- Modify: `LaunchPad.Companion\Program.cs`

- [ ] **Step 1: Implement Program.cs**

Write `V:\Projects\LaunchPad\LaunchPad.Companion\Program.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
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
        Console.WriteLine("[LaunchPad Companion] Starting...");

        // Companion connects as CLIENT to the widget's App Service.
        // The App Service connection is bidirectional: the widget (server side)
        // sends requests via SendMessageAsync, and the companion handles them
        // via RequestReceived. This is the standard Desktop Bridge pattern.
        _connection = new AppServiceConnection
        {
            AppServiceName = "com.launchpad.service",
            PackageFamilyName = Package.Current.Id.FamilyName
        };
        _connection.RequestReceived += OnRequestReceived;
        _connection.ServiceClosed += (_, _) =>
        {
            Console.WriteLine("[LaunchPad Companion] Service closed. Exiting.");
            ExitEvent.Set();
        };

        var status = await _connection.OpenAsync();
        if (status != AppServiceConnectionStatus.Success)
        {
            Console.WriteLine($"[LaunchPad Companion] Failed to connect: {status}");
            return;
        }

        Console.WriteLine("[LaunchPad Companion] Connected to App Service. Waiting for requests...");
        ExitEvent.WaitOne();
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
}
```

- [ ] **Step 2: Verify companion builds**

```bash
cd V:/Projects/LaunchPad
dotnet build LaunchPad.Companion/LaunchPad.Companion.csproj
```

Expected: Build succeeds. Note: The `Package.Current` call will fail at runtime outside a packaged context, which is expected — it only runs inside the MSIX package.

- [ ] **Step 3: Commit**

```bash
cd V:/Projects/LaunchPad
git add LaunchPad.Companion/Program.cs
git commit -m "feat: add companion App Service host with request dispatching"
```

---

## Task 6: Package Manifest

Write the Package.appxmanifest for the WAPPROJ with Game Bar widget extension, App Service, desktop extension for the companion, and proxy/stub registrations.

**Files:**
- Create: `LaunchPad.Package\Package.appxmanifest`

- [ ] **Step 1: Write the manifest**

Write `V:\Projects\LaunchPad\LaunchPad.Package\Package.appxmanifest`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
         xmlns:mp="http://schemas.microsoft.com/appx/2014/phone/manifest"
         xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
         xmlns:uap3="http://schemas.microsoft.com/appx/manifest/uap/windows10/3"
         xmlns:desktop="http://schemas.microsoft.com/appx/manifest/desktop/windows10"
         xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
         IgnorableNamespaces="uap uap3 mp desktop rescap">

  <Identity Name="LaunchPad" Publisher="CN=Developer" Version="1.0.0.0" />

  <mp:PhoneIdentity PhoneProductId="00000000-0000-0000-0000-000000000000"
                     PhonePublisherId="00000000-0000-0000-0000-000000000000" />

  <Properties>
    <DisplayName>LaunchPad</DisplayName>
    <PublisherDisplayName>Developer</PublisherDisplayName>
    <Logo>Images\StoreLogo.png</Logo>
  </Properties>

  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" MaxVersionTested="10.0.22621.0" />
  </Dependencies>

  <Resources>
    <Resource Language="x-generate" />
  </Resources>

  <Applications>
    <Application Id="App"
                 Executable="LaunchPad.Widget\LaunchPad.Widget.exe"
                 EntryPoint="LaunchPad.Widget.App">
      <uap:VisualElements DisplayName="LaunchPad"
                          Description="Game Bar app launcher widget"
                          Square150x150Logo="Images\Square150x150Logo.png"
                          Square44x44Logo="Images\Square44x44Logo.png"
                          BackgroundColor="transparent"
                          AppListEntry="none">
        <uap:DefaultTile Wide310x150Logo="Images\Wide310x150Logo.png" />
      </uap:VisualElements>

      <Extensions>
        <!-- Game Bar Widget -->
        <uap3:Extension Category="windows.appExtension">
          <uap3:AppExtension Name="microsoft.gameBarUIExtension"
                             Id="LaunchPadWidget"
                             DisplayName="LaunchPad"
                             Description="Quick app launcher grid"
                             PublicFolder="GameBar">
            <uap3:Properties>
              <GameBarWidget Type="Standard">
                <HomeMenuVisible>true</HomeMenuVisible>
                <PinningSupported>true</PinningSupported>
                <ActivateAfterInstall>true</ActivateAfterInstall>
                <FavoriteAfterInstall>true</FavoriteAfterInstall>
                <Window>
                  <AllowForegroundTransparency>true</AllowForegroundTransparency>
                  <Size>
                    <Height>350</Height>
                    <Width>400</Width>
                    <MinHeight>250</MinHeight>
                    <MinWidth>300</MinWidth>
                    <MaxHeight>500</MaxHeight>
                    <MaxWidth>600</MaxWidth>
                  </Size>
                  <ResizeSupported>
                    <Horizontal>true</Horizontal>
                    <Vertical>true</Vertical>
                  </ResizeSupported>
                </Window>
              </GameBarWidget>
            </uap3:Properties>
          </uap3:AppExtension>
        </uap3:Extension>

        <!-- App Service for companion communication -->
        <uap:Extension Category="windows.appService">
          <uap:AppService Name="com.launchpad.service" />
        </uap:Extension>

        <!-- Full Trust Companion Process -->
        <desktop:Extension Category="windows.fullTrustProcess"
                           Executable="LaunchPad.Companion\LaunchPad.Companion.exe" />
      </Extensions>
    </Application>
  </Applications>

  <Capabilities>
    <Capability Name="internetClient" />
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>

  <!-- Proxy/Stub for Game Bar SDK interop -->
  <Extensions>
    <Extension Category="windows.activatableClass.proxyStub">
      <ProxyStub ClassId="00000355-0000-0000-C000-000000000046">
        <Path>Microsoft.Gaming.XboxGameBar.winmd</Path>
        <Interface Name="Microsoft.Gaming.XboxGameBar.Private.IXboxGameBarWidgetHost" InterfaceId="5D12BC93-212B-4B9F-9091-76B73BF56525" />
        <Interface Name="Microsoft.Gaming.XboxGameBar.Private.IXboxGameBarWidgetHost2" InterfaceId="28717C8B-D8E8-47A8-AF47-A1D5263BAE9B" />
        <Interface Name="Microsoft.Gaming.XboxGameBar.Private.IXboxGameBarWidgetHost3" InterfaceId="3F5A3F12-C1E4-4942-B80D-3117BC948E29" />
        <Interface Name="Microsoft.Gaming.XboxGameBar.Private.IXboxGameBarWidgetHost4" InterfaceId="FA696D9E-2501-4B01-B26F-4BB85344740F" />
        <Interface Name="Microsoft.Gaming.XboxGameBar.Private.IXboxGameBarWidgetHost5" InterfaceId="A6C878CC-2B08-4B94-B1C3-222C6A913F3C" />
        <Interface Name="Microsoft.Gaming.XboxGameBar.Private.IXboxGameBarWidgetHost6" InterfaceId="CE6F0D73-C44F-4BBD-9652-A0FC52C37A34" />
        <Interface Name="Microsoft.Gaming.XboxGameBar.Private.IXboxGameBarWidgetPrivate" InterfaceId="22ABA97F-FB0F-4439-9BDD-2C67B2D5AA8F" />
        <Interface Name="Microsoft.Gaming.XboxGameBar.Private.IXboxGameBarWidgetPrivate2" InterfaceId="B2F7DB8C-7540-48DA-9B46-4E60CE0D9DEB" />
        <Interface Name="Microsoft.Gaming.XboxGameBar.Private.IXboxGameBarWidgetPrivate3" InterfaceId="4FB89FB6-7CB8-489D-8408-2269E6C733A1" />
        <Interface Name="Microsoft.Gaming.XboxGameBar.Private.IXboxGameBarWidgetPrivate4" InterfaceId="5638D65A-3733-48CC-90E5-984688D62786" />
        <Interface Name="Microsoft.Gaming.XboxGameBar.Private.IXboxGameBarWidgetControlHost" InterfaceId="C309CAC7-8435-4082-8F37-784523747047" />
        <Interface Name="Microsoft.Gaming.XboxGameBar.Private.IXboxGameBarWidgetForegroundWorkerHost" InterfaceId="DDB52B57-FA83-420C-AFDE-6FA556E18B83" />
        <Interface Name="Microsoft.Gaming.XboxGameBar.Private.IXboxGameBarWidgetForegroundWorkerPrivate" InterfaceId="42BACDFC-BB28-4E71-99B4-24C034C7B7E0" />
        <Interface Name="Microsoft.Gaming.XboxGameBar.Private.IXboxGameBarWidgetActivatedEventArgsPrivate" InterfaceId="782535A7-9407-4572-BFCB-316B4086F102" />
        <Interface Name="Microsoft.Gaming.XboxGameBar.Private.IXboxGameBarNavigationKeyCombo" InterfaceId="5EEA3DBF-09BB-42A5-B491-CF561E33C172" />
        <Interface Name="Microsoft.Gaming.XboxGameBar.Private.IXboxGameBarAppTargetHost" InterfaceId="38CDC43C-0A0E-4B3B-BBD3-A581AE220D53" />
        <Interface Name="Microsoft.Gaming.XboxGameBar.Private.IXboxGameBarAppTargetInfo" InterfaceId="D7689E93-5587-47D1-A42E-78D16B2FA807" />
        <Interface Name="Microsoft.Gaming.XboxGameBar.Private.IXboxGameBarActivityHost" InterfaceId="2B113C9B-E370-49B2-A20B-83E0F5737577" />
        <Interface Name="Microsoft.Gaming.XboxGameBar.Private.IXboxGameBarHotkeyManagerHost" InterfaceId="F6225A53-B34C-4833-9511-AA377B43316F" />
        <Interface Name="Microsoft.Gaming.XboxGameBar.Private.IXboxGameBarWidgetAuthHost" InterfaceId="DC263529-B12F-469E-BB35-B94069F5B15A" />
        <Interface Name="Microsoft.Gaming.XboxGameBar.Private.IXboxGameBarWidgetNotificationHost" InterfaceId="6F68D392-E4A9-46F7-A024-5275BC2FE7BA" />
        <Interface Name="Microsoft.Gaming.XboxGameBar.Private.IXboxGameBarWidgetNotificationPrivate" InterfaceId="C94C8DC8-C8B5-4560-AF6E-A588B558213A" />
      </ProxyStub>
    </Extension>
  </Extensions>
</Package>
```

- [ ] **Step 2: Create placeholder tile images**

Create minimal PNG placeholder images in `V:\Projects\LaunchPad\LaunchPad.Package\Images\`:
- `StoreLogo.png` (50x50)
- `Square150x150Logo.png` (150x150)
- `Square44x44Logo.png` (44x44)
- `Wide310x150Logo.png` (310x150)

These can be simple colored squares. Replace with designed assets later.

- [ ] **Step 3: Commit**

```bash
cd V:/Projects/LaunchPad
git add LaunchPad.Package/
git commit -m "feat: add package manifest with Game Bar widget and desktop extension declarations"
```

---

## Task 7: Widget — App Activation

Implement the Game Bar protocol activation handler and App Service connection management.

**Files:**
- Create: `LaunchPad.Widget\App.xaml`
- Create: `LaunchPad.Widget\App.xaml.cs`

- [ ] **Step 1: Write App.xaml**

Write `V:\Projects\LaunchPad\LaunchPad.Widget\App.xaml`:

```xml
<Application
    x:Class="LaunchPad.Widget.App"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
</Application>
```

- [ ] **Step 2: Write App.xaml.cs**

Write `V:\Projects\LaunchPad\LaunchPad.Widget\App.xaml.cs`:

```csharp
using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Microsoft.Gaming.XboxGameBar;

namespace LaunchPad.Widget;

sealed partial class App : Application
{
    private XboxGameBarWidget? _widget;
    private AppServiceConnection? _companionConnection;
    private BackgroundTaskDeferral? _appServiceDeferral;

    public static AppServiceConnection? CompanionConnection { get; private set; }

    public App()
    {
        this.InitializeComponent();
        this.Suspending += OnSuspending;
    }

    protected override void OnActivated(IActivatedEventArgs args)
    {
        if (args.Kind == ActivationKind.Protocol)
        {
            var protocolArgs = args as IProtocolActivatedEventArgs;
            if (protocolArgs?.Uri.SchemeName == "ms-gamebarwidget")
            {
                var widgetArgs = args as XboxGameBarWidgetActivatedEventArgs;
                if (widgetArgs != null && widgetArgs.IsLaunchActivation)
                {
                    var rootFrame = new Frame();
                    Window.Current.Content = rootFrame;

                    _widget = new XboxGameBarWidget(
                        widgetArgs,
                        Window.Current.CoreWindow,
                        rootFrame);

                    rootFrame.Navigate(typeof(LaunchPadWidget));
                    Window.Current.Activate();
                }
            }
        }
    }

    protected override void OnBackgroundActivated(BackgroundActivatedEventArgs args)
    {
        base.OnBackgroundActivated(args);

        if (args.TaskInstance.TriggerDetails is AppServiceTriggerDetails details)
        {
            _appServiceDeferral = args.TaskInstance.GetDeferral();
            _companionConnection = details.AppServiceConnection;
            CompanionConnection = _companionConnection;

            args.TaskInstance.Canceled += (_, _) =>
            {
                _appServiceDeferral?.Complete();
                CompanionConnection = null;
            };
        }
    }

    private void OnSuspending(object sender, SuspendingEventArgs e)
    {
        var deferral = e.SuspendingOperation.GetDeferral();
        _widget = null;
        CompanionConnection = null;
        deferral.Complete();
    }
}
```

- [ ] **Step 3: Commit**

```bash
cd V:/Projects/LaunchPad
git add LaunchPad.Widget/App.xaml LaunchPad.Widget/App.xaml.cs
git commit -m "feat: add Game Bar protocol activation and App Service connection management"
```

---

## Task 8: Widget — Companion Client

Wrap the App Service communication in a clean async API for the UI to call.

**Files:**
- Create: `LaunchPad.Widget\Services\CompanionClient.cs`

- [ ] **Step 1: Write CompanionClient**

Write `V:\Projects\LaunchPad\LaunchPad.Widget\Services\CompanionClient.cs`:

```csharp
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace LaunchPad.Widget.Services;

public static class CompanionClient
{
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
}
```

- [ ] **Step 2: Commit**

```bash
cd V:/Projects/LaunchPad
git add LaunchPad.Widget/Services/CompanionClient.cs
git commit -m "feat: add CompanionClient wrapper for App Service communication"
```

---

## Task 9: Widget — Grid UI

Build the main widget view: a 4-column grid of app tiles with icons and labels.

**Files:**
- Create: `LaunchPad.Widget\Models\LaunchItem.cs`
- Create: `LaunchPad.Widget\LaunchPadWidget.xaml`
- Create: `LaunchPad.Widget\LaunchPadWidget.xaml.cs`

- [ ] **Step 1: Create the view model**

Write `V:\Projects\LaunchPad\LaunchPad.Widget\Models\LaunchItem.cs`:

```csharp
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI.Xaml.Media.Imaging;

namespace LaunchPad.Widget.Models;

public class LaunchItem : INotifyPropertyChanged
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Path { get; set; } = "";
    public string? Args { get; set; }
    public string? CustomIconPath { get; set; }

    private BitmapImage? _iconSource;
    public BitmapImage? IconSource
    {
        get => _iconSource;
        set { _iconSource = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

- [ ] **Step 2: Write the XAML layout**

Write `V:\Projects\LaunchPad\LaunchPad.Widget\LaunchPadWidget.xaml`:

```xml
<Page
    x:Class="LaunchPad.Widget.LaunchPadWidget"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:models="using:LaunchPad.Widget.Models"
    Background="Transparent">

    <Grid Padding="8">
        <!-- Normal state: grid of items -->
        <GridView x:Name="ItemsGrid"
                  ItemsSource="{x:Bind Items}"
                  SelectionMode="None"
                  IsItemClickEnabled="True"
                  ItemClick="OnItemClick"
                  Visibility="Visible">
            <GridView.ItemsPanel>
                <ItemsPanelTemplate>
                    <ItemsWrapGrid MaximumRowsOrColumns="4" Orientation="Horizontal"
                                   ItemWidth="88" ItemHeight="88" />
                </ItemsPanelTemplate>
            </GridView.ItemsPanel>
            <GridView.ItemTemplate>
                <DataTemplate x:DataType="models:LaunchItem">
                    <Grid Width="80" Height="80"
                          CornerRadius="4" Padding="4"
                          Background="{ThemeResource SystemControlBackgroundListLowBrush}"
                          PointerEntered="OnTilePointerEntered"
                          PointerExited="OnTilePointerExited">
                        <StackPanel VerticalAlignment="Center"
                                    HorizontalAlignment="Center"
                                    Spacing="4">
                            <Image Source="{x:Bind IconSource, Mode=OneWay}"
                                   Width="36" Height="36"
                                   HorizontalAlignment="Center"
                                   Stretch="Uniform" />
                            <TextBlock Text="{x:Bind Name}"
                                       FontSize="11"
                                       TextAlignment="Center"
                                       TextTrimming="CharacterEllipsis"
                                       MaxLines="1"
                                       Foreground="{ThemeResource SystemControlForegroundBaseHighBrush}" />
                        </StackPanel>
                    </Grid>
                </DataTemplate>
            </GridView.ItemTemplate>
        </GridView>

        <!-- Empty/error state -->
        <StackPanel x:Name="EmptyState"
                    Visibility="Collapsed"
                    VerticalAlignment="Center"
                    HorizontalAlignment="Center"
                    Spacing="8"
                    Padding="16">
            <TextBlock x:Name="EmptyStateTitle"
                       Text="No apps configured"
                       FontSize="14" FontWeight="SemiBold"
                       TextAlignment="Center"
                       Foreground="{ThemeResource SystemControlForegroundBaseHighBrush}" />
            <TextBlock x:Name="EmptyStateMessage"
                       FontSize="11"
                       TextAlignment="Center"
                       TextWrapping="Wrap"
                       Foreground="{ThemeResource SystemControlForegroundBaseMediumBrush}" />
        </StackPanel>
    </Grid>
</Page>
```

- [ ] **Step 3: Write the code-behind**

Write `V:\Projects\LaunchPad\LaunchPad.Widget\LaunchPadWidget.xaml.cs`:

```csharp
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using LaunchPad.Shared;
using LaunchPad.Widget.Models;
using LaunchPad.Widget.Services;

namespace LaunchPad.Widget;

public sealed partial class LaunchPadWidget : Page
{
    public ObservableCollection<LaunchItem> Items { get; } = new();

    public LaunchPadWidget()
    {
        this.InitializeComponent();
        this.Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Start companion process
        try
        {
            await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
            // Give companion time to connect
            await Task.Delay(500);
        }
        catch (Exception)
        {
            // Companion may already be running
        }

        await LoadConfigAsync();
    }

    private async Task LoadConfigAsync()
    {
        var configPath = ConfigLoader.GetDefaultConfigPath();
        var result = ConfigLoader.Load(configPath);

        if (result.Status == ConfigLoadStatus.FileNotFound)
        {
            ShowEmptyState("No config file found",
                $"Create a config.json at:\n{configPath}");
            return;
        }

        if (result.Status == ConfigLoadStatus.ParseError)
        {
            ShowEmptyState("Invalid config file",
                $"JSON parse error:\n{result.ErrorMessage}");
            return;
        }

        if (result.Config == null || result.Config.Items.Count == 0)
        {
            ShowEmptyState("No apps configured",
                $"Add items to:\n{configPath}");
            return;
        }

        Items.Clear();
        foreach (var item in result.Config.Items)
        {
            var launchItem = new LaunchItem
            {
                Name = item.Name,
                Type = item.Type.ToString().ToLowerInvariant(),
                Path = item.Path,
                Args = item.Args,
                CustomIconPath = item.Icon
            };
            Items.Add(launchItem);
        }

        ItemsGrid.Visibility = Visibility.Visible;
        EmptyState.Visibility = Visibility.Collapsed;

        // Load icons in the background
        await LoadIconsAsync();
    }

    private async Task LoadIconsAsync()
    {
        foreach (var item in Items)
        {
            string? iconPath = null;

            if (item.CustomIconPath != null)
            {
                iconPath = item.CustomIconPath;
            }
            else if (item.Type == "exe")
            {
                iconPath = await CompanionClient.ExtractIconAsync(item.Path);
            }
            else if (item.Type == "url")
            {
                iconPath = await CompanionClient.FetchFaviconAsync(item.Path);
            }

            if (iconPath != null)
            {
                try
                {
                    var uri = new Uri(iconPath);
                    item.IconSource = new BitmapImage(uri);
                }
                catch
                {
                    SetDefaultIcon(item);
                }
            }
            else
            {
                SetDefaultIcon(item);
            }
        }
    }

    private void SetDefaultIcon(LaunchItem item)
    {
        var assetName = item.Type == "url" ? "DefaultGlobe.png" : "DefaultApp.png";
        item.IconSource = new BitmapImage(new Uri($"ms-appx:///Assets/{assetName}"));
    }

    private void ShowEmptyState(string title, string message)
    {
        ItemsGrid.Visibility = Visibility.Collapsed;
        EmptyState.Visibility = Visibility.Visible;
        EmptyStateTitle.Text = title;
        EmptyStateMessage.Text = message;
    }

    private async void OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is LaunchItem item)
        {
            var success = await CompanionClient.LaunchAsync(item.Type, item.Path, item.Args);

            // Brief visual feedback: flash the clicked tile
            if (sender is GridView gridView)
            {
                var container = gridView.ContainerFromItem(item) as GridViewItem;
                if (container != null)
                {
                    var grid = FindChild<Grid>(container);
                    if (grid != null)
                    {
                        var originalBrush = grid.Background;
                        grid.Background = success
                            ? new SolidColorBrush(Windows.UI.Colors.Green) { Opacity = 0.3 }
                            : new SolidColorBrush(Windows.UI.Colors.Red) { Opacity = 0.3 };

                        await Task.Delay(200);
                        grid.Background = originalBrush;
                    }
                }
            }
        }
    }

    private void OnTilePointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid grid)
            grid.Background = (Brush)Resources["SystemControlBackgroundListMediumBrush"]
                ?? new SolidColorBrush(Windows.UI.Colors.Gray) { Opacity = 0.2 };
    }

    private void OnTilePointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid grid)
            grid.Background = (Brush)Resources["SystemControlBackgroundListLowBrush"]
                ?? new SolidColorBrush(Windows.UI.Colors.Transparent);
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found) return found;
            var result = FindChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }
}
```

- [ ] **Step 4: Commit**

```bash
cd V:/Projects/LaunchPad
git add LaunchPad.Widget/Models/LaunchItem.cs LaunchPad.Widget/LaunchPadWidget.xaml LaunchPad.Widget/LaunchPadWidget.xaml.cs
git commit -m "feat: add LaunchPad grid widget UI with icon loading and launch support"
```

---

## Task 10: Sample Config & Integration Verification

Create the sample config file, build the full solution, and verify everything works.

**Files:**
- Create: `LaunchPad.Widget\config.sample.json`

- [ ] **Step 1: Create sample config**

Write `V:\Projects\LaunchPad\LaunchPad.Widget\config.sample.json`:

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
      "path": "spotify:",
      "args": null,
      "icon": null
    },
    {
      "name": "Xbox",
      "type": "store",
      "path": "xbox:",
      "args": null,
      "icon": null
    }
  ]
}
```

- [ ] **Step 2: Run all unit tests**

```bash
cd V:/Projects/LaunchPad
dotnet test LaunchPad.Tests -v normal
```

Expected: All tests PASS (ConfigModels: 7, LaunchHandler: 5, IconExtractor: 5 = 17 total).

- [ ] **Step 3: Build the full solution**

Open `V:\Projects\LaunchPad\LaunchPad.sln` in Visual Studio 2022. Set `LaunchPad.Package` as the startup project. Build in Debug|x64 configuration.

If the UWP project has build issues:
- Ensure the UWP workload is installed in VS
- Ensure Windows SDK 10.0.19041.0 is installed
- Check NuGet package restore completed
- Verify `Microsoft.Gaming.XboxGameBar` NuGet package is available

Alternatively, build from command line:

```bash
msbuild V:/Projects/LaunchPad/LaunchPad.sln /p:Configuration=Debug /p:Platform=x64 /restore
```

- [ ] **Step 4: Deploy and test**

1. Deploy the package from Visual Studio (Debug > Start Without Debugging, or right-click LaunchPad.Package > Deploy)
2. Copy `config.sample.json` to `%LOCALAPPDATA%\LaunchPad\config.json`
3. Open Game Bar with `Win+G`
4. Find "LaunchPad" in the widget list and open it
5. Verify the grid shows 6 items from the sample config
6. Click a tile (e.g., Notepad) and verify it launches
7. Verify URL items open in the default browser
8. Verify store/protocol items attempt to launch

- [ ] **Step 5: Commit**

```bash
cd V:/Projects/LaunchPad
git add LaunchPad.Widget/config.sample.json
git commit -m "feat: add sample config and complete v1 integration"
```

---

## Verification Checklist

After all tasks are complete, verify against the spec:

- [ ] Widget appears in Game Bar widget list as "LaunchPad"
- [ ] Grid renders with 4-column layout
- [ ] Icons auto-extracted from EXE files
- [ ] Favicons fetched for URL items
- [ ] Custom icon override works (set `icon` in config)
- [ ] Clicking EXE tiles launches the application
- [ ] Clicking URL tiles opens in default browser
- [ ] Clicking Store tiles opens the protocol handler
- [ ] Widget scrolls vertically with > 12 items
- [ ] Empty/missing config shows helpful error message
- [ ] Widget respects Game Bar dark/light theme
- [ ] Widget is pinnable (stays visible when Game Bar dismissed)
