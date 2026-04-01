#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Installs LaunchDeck Game Bar widget.
.DESCRIPTION
    Installs the developer certificate to Trusted People store,
    then installs the MSIX package. Requires administrator privileges.
#>

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$cert = Get-ChildItem "$scriptDir\*.cer" | Select-Object -First 1
$msix = Get-ChildItem "$scriptDir\*.msix" | Select-Object -First 1

if (-not $cert) { Write-Error "No .cer file found in $scriptDir"; exit 1 }
if (-not $msix) { Write-Error "No .msix file found in $scriptDir"; exit 1 }

Write-Host "Installing certificate: $($cert.Name)" -ForegroundColor Cyan
Import-Certificate -FilePath $cert.FullName -CertStoreLocation "Cert:\LocalMachine\TrustedPeople" | Out-Null
Write-Host "Certificate installed." -ForegroundColor Green

Write-Host "Installing package: $($msix.Name)" -ForegroundColor Cyan
Add-AppxPackage -Path $msix.FullName
Write-Host "LaunchDeck installed. Open Game Bar (Win+G) and enable the widget." -ForegroundColor Green
