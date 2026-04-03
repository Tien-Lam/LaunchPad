#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Uninstalls LaunchDeck Game Bar widget and cleans up all data.
.DESCRIPTION
    Removes the MSIX package, developer certificates, config file,
    cached icons, and companion log. Requires administrator privileges.
#>

$ErrorActionPreference = 'SilentlyContinue'

$procs = Get-Process -Name 'LaunchDeck.Companion', 'LaunchDeck.Widget' -ErrorAction SilentlyContinue
if ($procs) {
    Write-Host "Stopping running LaunchDeck processes..." -ForegroundColor Yellow
    $procs | Stop-Process -Force
    Start-Sleep -Seconds 1
}

Write-Host "Removing LaunchDeck package..." -ForegroundColor Cyan
Get-AppxPackage *LaunchDeck* | Remove-AppxPackage
Write-Host "Package removed." -ForegroundColor Green

Write-Host "Removing developer certificates..." -ForegroundColor Cyan
$stores = @('Cert:\LocalMachine\TrustedPeople', 'Cert:\LocalMachine\Root', 'Cert:\CurrentUser\My')
$subjects = @('CN=Developer', 'CN=E37AAF35-F870-4E74-8486-74BED9927C48')
$removed = 0
foreach ($store in $stores) {
    foreach ($subject in $subjects) {
        $certs = Get-ChildItem $store | Where-Object { $_.Subject -eq $subject }
        if ($certs) {
            $certs | Remove-Item
            $removed += $certs.Count
        }
    }
}
if ($removed -gt 0) {
    Write-Host "$removed certificate(s) removed." -ForegroundColor Green
} else {
    Write-Host "No certificates found." -ForegroundColor Yellow
}

Write-Host "Removing config and cached data..." -ForegroundColor Cyan
Remove-Item "$env:LOCALAPPDATA\LaunchDeck" -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "Data removed." -ForegroundColor Green

Write-Host ""
Write-Host "LaunchDeck fully uninstalled." -ForegroundColor Green
