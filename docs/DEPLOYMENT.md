# LaunchDeck Deployment Guide

## Solution Structure

The solution (`LaunchDeck.sln`) contains five projects:

| Project | Type | Target | Purpose |
|---------|------|--------|---------|
| `LaunchDeck.Shared` | .NET Standard 2.0 class library | `netstandard2.0` | Config models, JSON serialization (System.Text.Json 8.0.5). Referenced by both Widget and Companion. |
| `LaunchDeck.Widget` | UWP XAML app | UAP (target 10.0.26100.0, min 10.0.19041.0) | Game Bar widget UI. Output type `AppContainerExe`. References Shared. |
| `LaunchDeck.Companion` | .NET 10 WinExe | `net10.0-windows10.0.19041.0` | Full-trust Win32 companion process. Uses WPF for editor windows and WindowsForms for file dialogs. References Shared. |
| `LaunchDeck.Tests` | .NET test project | `net10.0-windows10.0.19041.0` | Unit tests for Shared and Companion logic. |
| `LaunchDeck.Package` | Windows Application Packaging (WAPPROJ) | MSIX | Packages Widget + Companion into a single MSIX bundle for deployment. |

### Project Dependencies

```
LaunchDeck.Package (WAPPROJ)
  +-- LaunchDeck.Widget (UWP, entry point)
  |     +-- LaunchDeck.Shared (.NET Standard 2.0)
  +-- LaunchDeck.Companion (.NET 10, net10.0-windows10.0.19041.0)
        +-- LaunchDeck.Shared (.NET Standard 2.0)
```

The WAPPROJ declares `LaunchDeck.Widget.csproj` as the `EntryPointProjectUniqueName`. Both Widget and Companion are listed as `ProjectReference` items in the WAPPROJ.

---

## Build Pipeline

### Non-UWP projects (Shared, Companion, Tests)

These use the .NET SDK and can be built from the command line:

```bash
dotnet build LaunchDeck.Shared/LaunchDeck.Shared.csproj
dotnet build LaunchDeck.Companion/LaunchDeck.Companion.csproj
dotnet test  LaunchDeck.Tests/
```

### Full solution (requires Visual Studio / MSBuild)

The Widget project is a classic UWP project (not SDK-style) and requires MSBuild with the UWP workload. It cannot be built with `dotnet build`.

```bash
# In bash, use -p: instead of /p: to avoid shell path expansion
msbuild LaunchDeck.sln -p:Configuration=Debug -p:Platform=x64 /restore
```

In PowerShell or Developer Command Prompt, `/p:` syntax works:

```powershell
msbuild LaunchDeck.sln /p:Configuration=Debug /p:Platform=x64 /restore
```

The solution is configured for `Debug|x64` and `Release|x64` platform configurations. The Shared project builds as `Any CPU`; all other projects build as `x64`.

### Prerequisites

- Visual Studio 2022 with the **Universal Windows Platform development** workload
- Windows SDK 10.0.26100.0 (required as `TargetPlatformVersion`; minimum platform version is 10.0.19041.0)
- .NET 10 SDK (for Companion)
- NuGet packages:
  - `Microsoft.Gaming.XboxGameBar` 5.8.220627001 (Widget)
  - `Microsoft.NETCore.UniversalWindowsPlatform` 6.2.14 (Widget)
  - `System.Text.Json` 8.0.5 (Shared)
  - Companion has no explicit NuGet dependencies; `System.Drawing` is available implicitly via `<UseWindowsForms>true</UseWindowsForms>`

---

## Deployment

### Primary method: `deploy.ps1`

The `deploy.ps1` script is the primary deployment method. It performs these steps:

1. Kills any running `LaunchDeck.Companion` and `LaunchDeck.Widget` processes.
2. Builds the full solution with MSBuild (`AppxBundle=Never`, Debug|x64).
3. Locates the `.msix` (or `.msixbundle`) in `LaunchDeck.Package\AppPackages`.
4. Extracts the MSIX to a layout directory (`LaunchDeck.Package\bin\x64\Debug\AppX`) using `ZipFile`. For `.msixbundle` files, extracts the bundle first, then the inner `.msix`.
5. Removes any existing `LaunchDeck` package registration via `Remove-AppxPackage`.
6. Registers the extracted layout via `Add-AppxPackage -Register` (loose-file registration, no signing needed).

```powershell
.\deploy.ps1
```

### Alternative: Visual Studio F5

You can also deploy via Visual Studio's F5 (Start Debugging) or Deploy command. Set `LaunchDeck.Package` as the startup project and deploy.

### Steps (deploy.ps1)

1. Run `.\deploy.ps1` from the solution root.
2. Open Game Bar with `Win+G`. The LaunchDeck widget should appear in the widget menu.

### Uninstall

Run `.\Uninstall.ps1` to remove the registered package and clean up cached data.

---

## MSIX Package Structure

The WAPPROJ (`LaunchDeck.Package.wapproj`) produces a single MSIX package containing:

```
LaunchDeck.Package/
  Package.appxmanifest          -- App manifest (identity, extensions, capabilities)
  Images/                       -- Store and tile logos
    StoreLogo.png
    Square150x150Logo.png
    Square44x44Logo.png
    Wide310x150Logo.png
  LaunchDeck.Widget/             -- UWP widget binaries
    LaunchDeck.Widget.exe
    LaunchDeck.Widget.dll
    (XAML pages, assets, SDK WinMD files)
  LaunchDeck.Companion/          -- .NET 10 companion binaries (net10.0-windows10.0.19041.0)
    LaunchDeck.Companion.exe
    (runtime dependencies)
```

Package signing is disabled (`AppxPackageSigningEnabled=false`), which is appropriate for local development deployment via Visual Studio.

---

## Manifest Structure

The manifest (`Package.appxmanifest`) declares the following:

### Identity

```xml
<Identity Name="34667TienLongLam.LaunchDeck" Publisher="CN=E37AAF35-..." Version="1.0.6.0" />
```

Target: `Windows.Desktop`, minimum SDK `10.0.19041.0`, max tested `10.0.26100.0`.

The `AppListEntry` is set to `none` -- the app does not appear in the Start menu. It is only accessible through Game Bar.

### Application Entry Point

- **Executable:** `LaunchDeck.Widget\LaunchDeck.Widget.exe`
- **Entry point class:** `LaunchDeck.Widget.App`

### Extensions (registered under the Application element)

#### 1. Game Bar Widget (`windows.appExtension`)

```xml
<uap3:AppExtension Name="microsoft.gameBarUIExtension"
                   Id="LaunchDeckWidget"
                   DisplayName="LaunchDeck"
                   Description="Quick app launcher grid"
                   PublicFolder="GameBar">
```

Widget properties:

| Property | Value | Meaning |
|----------|-------|---------|
| `Type` | `Standard` | Standard Game Bar widget (not a background/performance widget) |
| `HomeMenuVisible` | `true` | Widget appears in Game Bar's home menu |
| `PinningSupported` | `true` | User can pin the widget to the overlay |
| `ActivateAfterInstall` | `true` | Widget activates immediately after package install |
| `FavoriteAfterInstall` | `true` | Widget is added to favorites on install |

Window sizing:

| Dimension | Default | Min | Max |
|-----------|---------|-----|-----|
| Height | 350 | 250 | 500 |
| Width | 400 | 300 | 600 |

Both horizontal and vertical resizing are enabled.

#### 2. App Service (`windows.appService`)

```xml
<uap:AppService Name="com.launchdeck.service" />
```

This is the IPC channel between the UWP widget and the full-trust companion process. The widget sends launch requests through this service; the companion executes them outside the app container.

#### 3. Full Trust Process (`windows.fullTrustProcess`)

```xml
<desktop:Extension Category="windows.fullTrustProcess"
                   Executable="LaunchDeck.Companion\LaunchDeck.Companion.exe" />
```

Declares the companion as a full-trust desktop process that runs outside the UWP sandbox. This is required to launch arbitrary EXE files and access the file system.

### Extensions (registered at Package level)

#### 4. Proxy/Stub for Game Bar SDK Interop

```xml
<Extension Category="windows.activatableClass.proxyStub">
  <ProxyStub ClassId="00000355-0000-0000-C000-000000000046">
    <Path>Microsoft.Gaming.XboxGameBar.winmd</Path>
    <!-- 22 interface registrations for IXboxGameBar* private interfaces -->
  </ProxyStub>
</Extension>
```

This registers 22 COM proxy/stub interfaces required for the Game Bar SDK to communicate with the widget across process boundaries. The interfaces include `IXboxGameBarWidgetHost` (versions 1-6), `IXboxGameBarWidgetPrivate` (versions 1-4), `IXboxGameBarWidgetControlHost`, `IXboxGameBarWidgetForegroundWorkerHost`, `IXboxGameBarWidgetForegroundWorkerPrivate`, `IXboxGameBarWidgetActivatedEventArgsPrivate`, `IXboxGameBarNavigationKeyCombo`, `IXboxGameBarAppTargetHost`, `IXboxGameBarAppTargetInfo`, `IXboxGameBarActivityHost`, `IXboxGameBarHotkeyManagerHost`, `IXboxGameBarWidgetAuthHost`, `IXboxGameBarWidgetNotificationHost`, and `IXboxGameBarWidgetNotificationPrivate`. These are standard boilerplate required by `Microsoft.Gaming.XboxGameBar` and should not be modified.

### Capabilities

| Capability | Type | Purpose |
|------------|------|---------|
| `internetClient` | Standard | Network access for URL launches |
| `runFullTrust` | Restricted | Required to launch the companion process via `FullTrustProcessLauncher` |

---

## Troubleshooting

### Stale builds / mysterious runtime errors

**Symptom:** Changes to XAML, config models, or companion logic do not take effect after building.

**Fix:** Perform a clean rebuild. Delete `bin/` and `obj/` directories across all projects, then rebuild:

```bash
# From solution root
rm -rf LaunchDeck.Widget/bin LaunchDeck.Widget/obj
rm -rf LaunchDeck.Companion/bin LaunchDeck.Companion/obj
rm -rf LaunchDeck.Shared/bin LaunchDeck.Shared/obj
rm -rf LaunchDeck.Package/bin LaunchDeck.Package/obj
msbuild LaunchDeck.sln -p:Configuration=Debug -p:Platform=x64 /restore
```

Or in Visual Studio: **Build > Clean Solution**, then **Build > Rebuild Solution**.

### Game Bar does not show the widget after re-registration

**Symptom:** After uninstalling and reinstalling the package, or after significant manifest changes, the widget does not appear in Game Bar's widget list.

**Fix:** Reboot Windows. Game Bar caches widget registrations and does not always pick up changes from redeployment alone. After reboot, deploy again via F5 if needed.

### Blank window on direct launch

**Symptom:** Launching `LaunchDeck.Widget.exe` directly (or clicking the app tile if one exists) shows a blank window that immediately closes.

**Explanation:** This is expected behavior. The widget is designed to run only inside Game Bar. The `App.OnLaunched` method detects that it was not activated by Game Bar and exits. The widget must be opened through Game Bar (`Win+G`).

### Companion process not starting

**Symptom:** Widget loads but cannot launch apps. App Service connection fails.

**Check:** Verify the `runFullTrust` restricted capability is present in the manifest. Verify the companion executable path in the `fullTrustProcess` extension matches the actual output path (`LaunchDeck.Companion\LaunchDeck.Companion.exe`).

### NuGet restore failures

**Symptom:** Build fails with missing package errors.

**Fix:** Ensure NuGet restore runs before build. Use the `/restore` flag with MSBuild, or run `nuget restore LaunchDeck.sln` separately. The Widget project uses `packages.config`-style references through the UWP SDK toolchain, not SDK-style `PackageReference` (though they are declared inline in the csproj).

## See Also

- [Architecture](ARCHITECTURE.md) -- project structure and inter-project dependencies
- [Testing](TESTING.md) -- run tests before deploying to catch regressions
