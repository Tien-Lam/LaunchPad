#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Uninstalls LaunchDeck Game Bar widget and cleans up all data.
.DESCRIPTION
    Removes the MSIX package, developer certificate, config file,
    and cached icons. Requires administrator privileges.
#>

$ErrorActionPreference = 'SilentlyContinue'

Write-Host "Removing LaunchDeck package..." -ForegroundColor Cyan
Get-AppxPackage *LaunchDeck* | Remove-AppxPackage
Write-Host "Package removed." -ForegroundColor Green

Write-Host "Removing developer certificate..." -ForegroundColor Cyan
$certs = Get-ChildItem "Cert:\LocalMachine\TrustedPeople" | Where-Object { $_.Subject -eq "CN=Developer" -and $_.FriendlyName -like "*LaunchDeck*" }
if ($certs) {
    $certs | Remove-Item
    Write-Host "Certificate removed." -ForegroundColor Green
} else {
    # Fall back to matching just CN=Developer if FriendlyName wasn't set
    $certs = Get-ChildItem "Cert:\LocalMachine\TrustedPeople" | Where-Object { $_.Subject -eq "CN=Developer" }
    if ($certs) {
        Write-Host "Found $($certs.Count) certificate(s) with CN=Developer."
        $confirm = Read-Host "Remove all? (y/n)"
        if ($confirm -eq 'y') {
            $certs | Remove-Item
            Write-Host "Certificate(s) removed." -ForegroundColor Green
        }
    } else {
        Write-Host "No certificate found." -ForegroundColor Yellow
    }
}

Write-Host "Removing config and cached data..." -ForegroundColor Cyan
Remove-Item "$env:LOCALAPPDATA\LaunchDeck" -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "Data removed." -ForegroundColor Green

Write-Host ""
Write-Host "LaunchDeck fully uninstalled." -ForegroundColor Green
