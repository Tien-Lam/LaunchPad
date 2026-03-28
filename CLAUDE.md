# LaunchPad - Xbox Game Bar Widget

## CRITICAL: Workflow Rules

**Before writing ANY code**, you MUST:
1. Run `export PATH="$PATH:/c/Users/lamti/AppData/Local/Programs/bd:/c/Program Files/dolt/bin"` (machine-specific)
2. Create a beads issue: `bd create --title="..." --type=bug|task|feature --priority=2`
3. Claim it: `bd update <id> --claim`

**After completing code for an issue**: `bd close <id>`

**NEVER use** TodoWrite, TaskCreate, or markdown task lists. Beads is the ONLY tracking system.

## Project Overview

Game Bar widget that launches apps (EXEs, URLs, Store apps) from a configurable grid overlay. UWP XAML widget + Win32 companion process (App Service IPC) in a single MSIX package.

## Project Structure

```
LaunchPad.Widget/       # UWP XAML widget (runs inside Game Bar)
LaunchPad.Companion/    # .NET 8 Win32 process (IPC handler, WPF editor, icon extraction)
LaunchPad.Shared/       # .NET Standard 2.0 library (ConfigLoader, config models)
LaunchPad.Tests/        # xUnit tests (references Shared + Companion)
LaunchPad.Package/      # WAPPROJ — MSIX packaging, manifest, deployment
docs/                   # Architecture, IPC, Config, UI, Deployment, Testing docs
```

## Docs

- [Architecture](docs/ARCHITECTURE.md) -- system overview, two-process design, project map
- [IPC Protocol](docs/IPC.md) -- App Service actions, request/response fields, sequence flows
- [Config](docs/CONFIG.md) -- JSON schema, item types, icon resolution, UWP path workaround
- [UI](docs/UI.md) -- dark theme palette, XAML structure, interactive states
- [Deployment](docs/DEPLOYMENT.md) -- build pipeline, VS deploy, manifest, troubleshooting
- [Testing](docs/TESTING.md) -- test coverage, boundaries, manual test checklist

## Beads Quick Reference

```bash
bd prime                 # Full workflow context (run at session start)
bd ready                 # Find unblocked work
bd create --title="..." --type=task --priority=2  # Create issue
bd update <id> --claim   # Claim and start work
bd close <id>            # Complete work
bd show <id>             # View issue details
```

## Sub-Agents

Always pass `model: "opus"` on every Agent tool call. Never use sonnet or haiku for delegated work.

## Testing Standards

- Run `dotnet test LaunchPad.Tests/` after any code change and before committing
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

- C# / UWP XAML (widget) / .NET 8 + WPF + WinForms (companion) / .NET Standard 2.0 (shared)
- Microsoft.Gaming.XboxGameBar NuGet
- Visual Studio 2022 + UWP workload + Windows SDK 19041+
- Windows Application Packaging Project (WAPPROJ)

## Gotchas

- **UWP sandbox**: Widget cannot access filesystem, spawn processes, or show Win32 dialogs. All of these go through the companion via IPC.
- **Icon loading**: Icons must be sent as base64 bytes over IPC. The widget cannot read files from `%LOCALAPPDATA%` due to AppContainer restrictions.
- **Path virtualization**: UWP sees `%LOCALAPPDATA%` as `Packages\<id>\LocalState`. `ConfigLoader.StripPackagePath()` corrects this so widget and companion resolve the same config path.
- **WPF + WinForms coexistence**: Both are enabled in the companion. Qualify `System.Windows.Forms.OpenFileDialog` to avoid ambiguity with `Microsoft.Win32.OpenFileDialog`.

## Build

```bash
# Non-UWP projects (shared, companion, tests)
dotnet build LaunchPad.Shared/LaunchPad.Shared.csproj
dotnet build LaunchPad.Companion/LaunchPad.Companion.csproj
dotnet test LaunchPad.Tests/

# Run a single test class or method
dotnet test LaunchPad.Tests/ --filter EditorModelTests
dotnet test LaunchPad.Tests/ --filter "EditorModelTests.AddExe_AppendsItemAndSelectsIt"

# Full solution (requires VS / MSBuild)
msbuild LaunchPad.sln /p:Configuration=Debug /p:Platform=x64 /restore
```

## Deploy

Deploy via Visual Studio (F5). Command-line `Add-AppxPackage` does not reliably register Game Bar widgets.
