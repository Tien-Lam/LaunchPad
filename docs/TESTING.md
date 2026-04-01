# Testing Guide

## Overview

LaunchDeck uses **xUnit 2.9** on **.NET 8** (`net8.0-windows10.0.19041.0`) for automated testing. Tests live in the `LaunchDeck.Tests` project, which references both `LaunchDeck.Shared` and `LaunchDeck.Companion`.

## Running Tests

```bash
dotnet test LaunchDeck.Tests/
```

To run with verbose output:

```bash
dotnet test LaunchDeck.Tests/ --logger "console;verbosity=detailed"
```

To run a specific test class:

```bash
dotnet test LaunchDeck.Tests/ --filter "FullyQualifiedName~LaunchHandlerTests"
```

Platform note: the csproj supports `x64`, `x86`, and `ARM`. The default `dotnet test` invocation uses AnyCPU / the host architecture, which works for all current tests.

## Test Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| xunit | 2.9.3 | Test framework |
| xunit.runner.visualstudio | 3.1.4 | VS Test Explorer integration |
| Microsoft.NET.Test.Sdk | 17.14.1 | Test host |
| coverlet.collector | 6.0.4 | Code coverage collection |

No mocking framework is used. All tests are direct unit tests against concrete implementations.

## Test Coverage by Area

### ConfigModelsTests (9 tests)

Tests the shared configuration model (`LaunchDeck.Shared.ConfigModels`) -- JSON serialization, deserialization, and the `ConfigLoader` utility.

| Test | What it verifies |
|------|-----------------|
| `Deserialize_ValidConfig_ReturnsItems` | Parses a config with two items; checks name, type, path, and that optional fields default to null |
| `Deserialize_WithOptionalFields_ParsesCorrectly` | Parses `args` and `icon` optional fields |
| `Deserialize_EmptyItems_ReturnsEmptyList` | Empty items array produces an empty list, not null |
| `Deserialize_AllTypes_ParsesCorrectly` | All three `LaunchItemType` enum values (`Exe`, `Url`, `Store`) deserialize from JSON strings |
| `ConfigLoader_MissingFile_ReturnsFileNotFound` | `ConfigLoader.Load` returns `FileNotFound` status for a nonexistent path |
| `ConfigLoader_ValidFile_ReturnsSuccess` | Writes a temp file, loads it, asserts `Success` status and correct item count |
| `ConfigLoader_MalformedJson_ReturnsParseError` | Malformed JSON returns `ParseError` status with a non-null error message |
| `ConfigLoader_Save_WritesValidJson` | Round-trips a config through `Save` then `Load`; verifies data integrity |
| `ConfigLoader_Save_PreservesAllFields` | Round-trip preserves optional fields (`Args`, `Icon`) |

Source under test: `LaunchDeck.Shared/ConfigModels.cs`

### LaunchHandlerTests (5 tests)

Tests the companion process launch logic (`LaunchDeck.Companion.LaunchHandler`), specifically the `BuildProcessStartInfo` static method.

| Test | What it verifies |
|------|-----------------|
| `BuildProcessStartInfo_Exe_SetsFileName` | EXE type sets `FileName`, empty `Arguments`, `UseShellExecute = true` |
| `BuildProcessStartInfo_ExeWithArgs_SetsArguments` | EXE type with args populates `Arguments` correctly |
| `BuildProcessStartInfo_Url_SetsFileNameToUrl` | URL type sets `FileName` to the URL, `UseShellExecute = true` |
| `BuildProcessStartInfo_Store_SetsFileNameToProtocol` | Store type sets `FileName` to the protocol URI |
| `BuildProcessStartInfo_UnknownType_ThrowsArgumentException` | Unknown type string throws `ArgumentException` |

Source under test: `LaunchDeck.Companion/LaunchHandler.cs`

### IconExtractorTests (5 tests)

Tests icon caching and extraction utilities (`LaunchDeck.Companion.IconExtractor`).

| Test | What it verifies |
|------|-----------------|
| `GetCacheFileName_ReturnsDeterministicHash` | Same path produces the same cache filename; filename ends with `.png` |
| `GetCacheFileName_DifferentPaths_DifferentNames` | Different EXE paths produce different cache filenames |
| `ExtractFromExe_ValidExe_SavesPng` | Extracts an icon from `notepad.exe` to a temp directory; verifies the output file exists |
| `ExtractFromExe_NonexistentExe_ReturnsFailure` | Nonexistent EXE path returns `Success = false` |
| `GetFaviconUrl_ExtractsDomain` | Extracts the domain from a full URL for favicon lookup |

Source under test: `LaunchDeck.Companion/IconExtractor.cs`

Note: `ExtractFromExe_ValidExe_SavesPng` depends on `C:\Windows\notepad.exe` being present on the machine. This test will fail in environments without a standard Windows installation (e.g., minimal containers).

### ExePickerTests (5 tests)

Tests the EXE picker logic (`LaunchDeck.Companion.ExePicker`) for display name extraction and config manipulation.

| Test | What it verifies |
|------|-----------------|
| `GetDisplayName_Notepad_ReturnsFileDescription` | Reads `FileDescription` from `notepad.exe` version info; expects `"Notepad"` |
| `GetDisplayName_NonexistentExe_ReturnsFileNameWithoutExtension` | Falls back to filename stem when EXE does not exist |
| `GetDisplayName_NullPath_ReturnsUnknown` | Null path returns `"Unknown"` |
| `AppendToConfig_AddsNewItem` | Adds a new EXE item to config; checks name, type, and path |
| `AppendToConfig_DoesNotAddDuplicate` | Adding the same path twice results in only one item |

Source under test: `LaunchDeck.Companion/ExePicker.cs`

Note: `GetDisplayName_Notepad_ReturnsFileDescription` reads version info from a real EXE on disk. It is Windows-specific.

## Test Patterns

This project uses a straightforward approach to testing:

- **Direct unit tests only.** No mocking framework (e.g., Moq, NSubstitute) is used. All tests call static methods or construct concrete objects directly.
- **Arrange-Act-Assert.** Each test follows a clear setup, execution, and assertion structure.
- **Temp files for I/O tests.** Tests that exercise file I/O (`ConfigLoader.Load`, `ConfigLoader.Save`, `IconExtractor.ExtractFromExe`) create temp files/directories and clean up in a `try/finally` block.
- **Known OS fixtures.** Some tests rely on `C:\Windows\notepad.exe` being present, which is a safe assumption on standard Windows installations but makes these tests non-portable.
- **Tuple return types for results.** Production code uses value tuples (e.g., `(bool Success, string? Error)`) and result objects (`ConfigLoadResult`) rather than exceptions for expected failure cases, and tests verify both the success and failure paths.

## What Cannot Be Unit Tested

Several areas of the codebase are not covered by automated tests due to runtime and environmental constraints:

### UWP Widget UI (`LaunchDeck.Widget`)

The widget project is a UWP XAML app that runs inside the Xbox Game Bar host process. It requires the Game Bar runtime to instantiate `XboxGameBarWidget` objects and render XAML. There is no headless UWP test host that can simulate this environment, so all widget UI behavior is manual-test only.

### App Service IPC

Communication between the widget and companion process uses Windows App Services (`Windows.ApplicationModel.AppService`). This requires:
- Both processes to be deployed via the MSIX package
- The App Service connection to be brokered by the OS package manager
- The companion to be registered as a background task

None of these conditions can be replicated in an xUnit test runner. The IPC layer must be tested end-to-end by deploying the full package.

### Icon Extraction (full pipeline)

While `GetCacheFileName` and `GetFaviconUrl` are pure functions that are unit-testable, the actual icon extraction from EXE files (`ExtractFromExe`) uses Win32 shell APIs and requires real EXE files on disk. The existing test covers `notepad.exe` as a known fixture, but this is an integration test by nature -- it touches the filesystem and calls platform APIs.

### Game Bar Integration

Registering the widget with Game Bar, handling activation, and managing the widget lifecycle all depend on the Game Bar host. These paths can only be tested by deploying the MSIX package and opening Game Bar.

### Process Launching (`LaunchHandler.Launch`)

The `Launch` method calls `Process.Start`, which actually spawns a process. `BuildProcessStartInfo` is tested in isolation, but verifying that a process was actually launched is a side-effectful integration concern that is not covered.

## Testing Boundaries

| Layer | Testable? | How |
|-------|-----------|-----|
| `LaunchDeck.Shared` (config models, loader, serialization) | Yes | xUnit unit tests |
| `LaunchDeck.Companion` (launch logic, icon cache naming, favicon URL, exe picker) | Yes | xUnit unit tests |
| `LaunchDeck.Companion` (icon extraction from real EXEs) | Partially | xUnit with known OS fixtures (`notepad.exe`) |
| `LaunchDeck.Companion` (process launching) | No | Side-effectful; tested manually |
| `LaunchDeck.Widget` (XAML UI, data binding, grid layout) | No | Requires Game Bar runtime; manual only |
| App Service IPC (widget <-> companion) | No | Requires MSIX deployment; manual only |
| MSIX packaging and activation | No | Requires deployment; manual only |

## Manual Testing Procedure

For areas that cannot be automated, follow this procedure:

### Prerequisites

- Visual Studio 2022 with UWP workload and Windows SDK 19041+
- Windows 10/11 with Xbox Game Bar enabled
- Developer Mode enabled in Windows Settings

### Deploy and Launch

1. Open `LaunchDeck.sln` in Visual Studio.
2. Set `LaunchDeck.Package` as the startup project.
3. Set platform to `x64` and configuration to `Debug`.
4. Press F5 (or Deploy without debugging via Ctrl+F5).
5. Open Xbox Game Bar with `Win+G`.
6. Find the LaunchDeck widget in the widget menu and pin it.

### Manual Test Checklist

- **Config loading:** Place a valid `config.json` in `%LOCALAPPDATA%\LaunchDeck\config.json`. Verify the widget displays the configured items in a grid.
- **EXE launch:** Click an EXE-type item. Verify the application starts.
- **URL launch:** Click a URL-type item. Verify the browser opens to the correct URL.
- **Store app launch:** Click a store-type item. Verify the store app launches.
- **Add EXE via picker:** Click the `+` button on the widget. Select an EXE file. Verify it appears in the grid and persists in `config.json`.
- **Icon display:** Verify EXE items show extracted icons and URL items show favicons.
- **Error handling:** Remove or corrupt `config.json`. Verify the widget shows an appropriate error state rather than crashing.
- **IPC resilience:** Kill the companion process while the widget is open. Verify the widget handles the disconnection gracefully.

## Code Coverage

The project includes `coverlet.collector` for coverage data. To generate a coverage report:

```bash
dotnet test LaunchDeck.Tests/ --collect:"XPlat Code Coverage"
```

Coverage results are written to `LaunchDeck.Tests/TestResults/`. Use a tool like `reportgenerator` to produce HTML reports from the generated Cobertura XML.

## See Also

- [Architecture](ARCHITECTURE.md) -- what is testable depends on the two-process architecture
- [Deployment](DEPLOYMENT.md) -- build the solution before running tests
