# LaunchDeck build + deploy script
# Run from project root in PowerShell

$ErrorActionPreference = 'Stop'

# Find MSBuild
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vsPath = & $vswhere -latest -property installationPath
$msbuild = Join-Path $vsPath 'MSBuild\Current\Bin\MSBuild.exe'

if (-not (Test-Path $msbuild)) {
    Write-Error "MSBuild not found at $msbuild"
    exit 1
}

Write-Host "Using MSBuild: $msbuild" -ForegroundColor Cyan

# Kill running LaunchDeck processes
$procs = Get-Process -Name 'LaunchDeck.Companion', 'LaunchDeck.Widget' -ErrorAction SilentlyContinue
if ($procs) {
    Write-Host "Stopping running LaunchDeck processes..." -ForegroundColor Yellow
    $procs | Stop-Process -Force
    Start-Sleep -Seconds 1
}

# Build full solution (produces .msix)
Write-Host "Building solution..." -ForegroundColor Cyan
& $msbuild LaunchDeck.sln -p:Configuration=Debug -p:Platform=x64 -restore -v:minimal
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit 1
}

# Extract MSIX to layout directory for loose-file registration
$msix = Join-Path $PSScriptRoot 'LaunchDeck.Package\AppPackages\LaunchDeck.Package_1.0.0.0_x64_Debug_Test\LaunchDeck.Package_1.0.0.0_x64_Debug.msix'
$layoutDir = Join-Path $PSScriptRoot 'LaunchDeck.Package\bin\x64\Debug\AppX'

if (-not (Test-Path $msix)) {
    Write-Error "MSIX not found at $msix"
    exit 1
}

Write-Host "Extracting package layout..." -ForegroundColor Cyan
if (Test-Path $layoutDir) {
    Remove-Item $layoutDir -Recurse -Force
}
New-Item $layoutDir -ItemType Directory | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($msix, $layoutDir)

# Remove existing registration, then re-register
$existing = Get-AppxPackage -Name 'LaunchDeck' -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Removing existing package..." -ForegroundColor Yellow
    Remove-AppxPackage $existing.PackageFullName
}

$manifest = Join-Path $layoutDir 'AppxManifest.xml'
Write-Host "Registering package..." -ForegroundColor Cyan
Add-AppxPackage -Register $manifest

Write-Host "Deployed successfully. Open Game Bar (Win+G) to use the widget." -ForegroundColor Green
