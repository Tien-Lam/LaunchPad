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
& $msbuild LaunchDeck.sln -p:Configuration=Debug -p:Platform=x64 -p:AppxBundle=Never -restore -v:minimal
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit 1
}

# Find MSIX in output (prefer .msix over .msixbundle for loose-file registration)
$pkg = Get-ChildItem -Path "$PSScriptRoot\LaunchDeck.Package\AppPackages" -Recurse -Include '*.msix' |
    Where-Object { $_.Name -match 'Debug' -and $_.Extension -eq '.msix' } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if (-not $pkg) {
    $pkg = Get-ChildItem -Path "$PSScriptRoot\LaunchDeck.Package\AppPackages" -Recurse -Include '*.msixbundle' |
        Where-Object { $_.Name -match 'Debug' } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
}
$layoutDir = Join-Path $PSScriptRoot 'LaunchDeck.Package\bin\x64\Debug\AppX'

if (-not $pkg) {
    Write-Error "No MSIX/MSIXBUNDLE found in LaunchDeck.Package\AppPackages"
    exit 1
}

Write-Host "Found package: $($pkg.Name)" -ForegroundColor Cyan

Write-Host "Extracting package layout..." -ForegroundColor Cyan
if (Test-Path $layoutDir) {
    Remove-Item $layoutDir -Recurse -Force
}
New-Item $layoutDir -ItemType Directory | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem

# Bundles contain an inner .msix — extract the bundle first, then the msix
if ($pkg.Extension -eq '.msixbundle') {
    $bundleDir = Join-Path $PSScriptRoot 'LaunchDeck.Package\bin\x64\Debug\Bundle'
    if (Test-Path $bundleDir) { Remove-Item $bundleDir -Recurse -Force }
    New-Item $bundleDir -ItemType Directory | Out-Null
    [System.IO.Compression.ZipFile]::ExtractToDirectory($pkg.FullName, $bundleDir)
    $innerMsix = Get-ChildItem $bundleDir -Filter '*.msix' | Select-Object -First 1
    [System.IO.Compression.ZipFile]::ExtractToDirectory($innerMsix.FullName, $layoutDir)
    Remove-Item $bundleDir -Recurse -Force
} else {
    [System.IO.Compression.ZipFile]::ExtractToDirectory($pkg.FullName, $layoutDir)
}

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
