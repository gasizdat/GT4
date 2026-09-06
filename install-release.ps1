#Requires -RunAsAdministrator
param(
  [Parameter(Mandatory = $true)][string]$Version,
  [string]$DownloadDir = "$env:USERPROFILE\Downloads"
)

$ErrorActionPreference = 'Stop'

$msixName = "GT4-$Version-win-x64.msix"
$cerName = "GT4-$Version-win-x64.cer"
$msixPath = Join-Path $DownloadDir $msixName
$cerPath = Join-Path $DownloadDir $cerName

gh release download "v$Version" --pattern "*.msix" --pattern "*.cer" --dir $DownloadDir --clobber

# A fresh container, not an upgrade: Add-AppxPackage over an existing install reuses it, which
# exercises the upgrade path instead of the first-run path a new Store customer actually hits.
Get-AppxPackage gasizdat.GenealogyTree | ForEach-Object { Remove-AppxPackage -Package $_.PackageFullName }

# Each release mints its own throwaway signing certificate, so this has to run every time,
# not just once — the previous release's trust entry does not cover the new one.
Import-Certificate -FilePath $cerPath -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null

Add-AppxPackage -Path $msixPath

Get-AppxPackage gasizdat.GenealogyTree | Select-Object Name, Version, PackageFullName, InstallLocation
