# Runs as the calling user throughout; only the certificate import below elevates, in its own
# process, so this needs no elevated shell to launch it.
param(
  [string]$Version,
  [string]$DownloadDir = "$env:USERPROFILE\Downloads",
  [string]$MsixPath,
  [string]$CerPath
)

if (-not $Version) {
  Write-Host @'
Sideload-installs a signed Genealogy Tree release build for pre-upload
verification, trusting the release's throwaway certificate elevated for
that one step only.

Usage: install-release.ps1 -Version <version> [-DownloadDir <dir>] [-MsixPath <path>] [-CerPath <path>]

Examples:

  # Download the release assets into Downloads, then install
  .\install-release.ps1 -Version 4.0.656.0

  # Files already downloaded elsewhere - skip gh entirely
  .\install-release.ps1 -Version 4.0.656.0 -MsixPath "C:\Users\gas\Downloads\GT4-4.0.656.0-win-x64.msix" -CerPath "C:\Users\gas\Downloads\GT4-4.0.656.0-win-x64.cer"

  # Download into a directory other than Downloads
  .\install-release.ps1 -Version 4.0.656.0 -DownloadDir "D:\GT4-releases"
'@
  exit 1
}

$ErrorActionPreference = 'Stop'

if (-not $MsixPath) { $MsixPath = Join-Path $DownloadDir "GT4-$Version-win-x64.msix" }
if (-not $CerPath) { $CerPath = Join-Path $DownloadDir "GT4-$Version-win-x64.cer" }

if (-not (Test-Path $MsixPath) -or -not (Test-Path $CerPath)) {
  New-Item -ItemType Directory -Force -Path $DownloadDir | Out-Null
  # --repo makes this independent of the caller's working directory: gh otherwise infers the
  # repository from the current directory's git remote, and has nothing to infer it from when
  # launched via -File from outside the repo (e.g. an elevated shell that opens elsewhere).
  gh release download "v$Version" --repo gasizdat/GT4 --pattern "*.msix" --pattern "*.cer" --dir $DownloadDir --clobber
  if (-not (Test-Path $MsixPath)) { throw "expected asset not found after download: $MsixPath" }
  if (-not (Test-Path $CerPath)) { throw "expected asset not found after download: $CerPath" }
}

# A fresh container, not an upgrade: Add-AppxPackage over an existing install reuses it, which
# exercises the upgrade path instead of the first-run path a new Store customer actually hits.
Get-AppxPackage gasizdat.GenealogyTree | ForEach-Object { Remove-AppxPackage -Package $_.PackageFullName }

# Trusting the certificate is the one step that needs admin rights, and each release mints its
# own throwaway certificate so it has to happen every time. Elevate only for this one command,
# in its own process, rather than the whole script — gh and git need the calling user's own PATH
# and credentials, which an elevated shell does not carry.
$certPathLiteral = "'" + ($CerPath -replace "'", "''") + "'"
$importCommand = "`$cert = Get-PfxCertificate -FilePath $certPathLiteral; " +
  "Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object { `$_.Subject -eq `$cert.Subject } | Remove-Item; " +
  "Import-Certificate -FilePath $certPathLiteral -CertStoreLocation Cert:\LocalMachine\TrustedPeople"
$encodedCommand = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($importCommand))
$importProcess = Start-Process powershell.exe -ArgumentList '-NoProfile', '-EncodedCommand', $encodedCommand -Verb RunAs -Wait -PassThru
if ($importProcess.ExitCode -ne 0) { throw "certificate import failed (exit code $($importProcess.ExitCode))" }

Add-AppxPackage -Path $MsixPath

Get-AppxPackage gasizdat.GenealogyTree | Select-Object Name, Version, PackageFullName, InstallLocation
