# LaunchDeck - Xbox Game Bar Widget

@AGENTS.md

## Project Overview

Game Bar widget that launches apps and URLs from a configurable grid overlay. UWP XAML widget + Win32 companion process (App Service IPC) in a single MSIX package.

## Project Structure

```
LaunchDeck.Widget/       # UWP XAML widget (runs inside Game Bar)
LaunchDeck.Companion/    # .NET 10 Win32 process (IPC handler, WPF editor, icon extraction)
LaunchDeck.Shared/       # .NET Standard 2.0 library (ConfigLoader, config models)
LaunchDeck.Tests/        # xUnit tests (references Shared + Companion)
LaunchDeck.Package/      # WAPPROJ — MSIX packaging, manifest, deployment
docs/                   # Architecture, IPC, Config, UI, Deployment, Testing docs
```

## Docs

- [Architecture](docs/ARCHITECTURE.md) -- system overview, two-process design, project map
- [IPC Protocol](docs/IPC.md) -- App Service actions, request/response fields, sequence flows
- [Config](docs/CONFIG.md) -- JSON schema, item types, icon resolution, UWP path workaround
- [UI](docs/UI.md) -- dark theme palette, XAML structure, interactive states
- [Deployment](docs/DEPLOYMENT.md) -- build pipeline, VS deploy, manifest, troubleshooting
- [Testing](docs/TESTING.md) -- test coverage, boundaries, manual test checklist

## Sub-Agents

Always pass `model: "opus"` on every Agent tool call. Never use sonnet or haiku for delegated work.

## Testing Standards

- Run `dotnet test LaunchDeck.Tests/` after any code change and before committing
- Every test must catch a real bug — if you can't name what would break, delete the test
- No false-confidence tests (tests that pass even if the code is broken)
- Extract logic from WPF/UWP code-behind into testable classes (e.g., `EditorModel`)
- Test patterns: xUnit, `Path.GetTempFileName()` with `try/finally` cleanup, `C:\Windows\notepad.exe` as known fixture
- UWP widget code is not unit-testable — use manual checklist in docs/TESTING.md

## Code Style

- Static classes for stateless services (`ConfigLoader`, `LaunchHandler`, `IconExtractor`)
- No dependency injection — keep it simple
- `internal` + `InternalsVisibleTo` for types that need testing but shouldn't be public API

## Tech Stack

- C# / UWP XAML (widget) / .NET 10 + WPF + WinForms (companion) / .NET Standard 2.0 (shared)
- Microsoft.Gaming.XboxGameBar NuGet
- Visual Studio 2022 + UWP workload + Windows SDK 19041+
- Windows Application Packaging Project (WAPPROJ)

## Gotchas

- **UWP sandbox**: Widget cannot access filesystem, spawn processes, or show Win32 dialogs. All of these go through the companion via IPC.
- **Icon loading**: Icons must be sent as base64 bytes over IPC. The widget cannot read files from `%LOCALAPPDATA%` due to AppContainer restrictions.
- **Path virtualization**: UWP sees `%LOCALAPPDATA%` as `Packages\<id>\LocalState`. `ConfigLoader.StripPackagePath()` corrects this so widget and companion resolve the same config path.
- **WPF + WinForms coexistence**: Both are enabled in the companion. Qualify `System.Windows.Forms.OpenFileDialog` to avoid ambiguity with `Microsoft.Win32.OpenFileDialog`.

## Working Documents

Superpowers specs and plans go in `.claude/working/` (gitignored), not `docs/`. These are working files for Claude, not project documentation. Beads tracks issues, git tracks changes, `docs/` has living documentation.

## Docs Maintenance

When changing IPC actions, UI behavior, or config schema, update the corresponding doc in `docs/` and `README.md`. Grep for the old behavior across all docs to catch stale references.

## Build

```bash
# Non-UWP projects (shared, companion, tests)
dotnet build LaunchDeck.Shared/LaunchDeck.Shared.csproj
dotnet build LaunchDeck.Companion/LaunchDeck.Companion.csproj
dotnet test LaunchDeck.Tests/

# Run a single test class or method
dotnet test LaunchDeck.Tests/ --filter EditorModelTests
dotnet test LaunchDeck.Tests/ --filter "EditorModelTests.AddExe_AppendsItemAndSelectsIt"

# Full solution (requires VS / MSBuild)
msbuild LaunchDeck.sln /p:Configuration=Debug /p:Platform=x64 /restore
```

## Deploy

```bash
powershell.exe -ExecutionPolicy Bypass -File deploy.ps1
```

The script kills running LaunchDeck processes, builds with MSBuild, and registers the package. After deploying, open Game Bar (Win+G) to use the widget.
