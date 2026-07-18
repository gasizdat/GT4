<#
.SYNOPSIS
  Builds, deploys and runs GT4.UI.App.DeviceTests on the attached Android device.

  The phone shows an install-approval prompt on every deploy - watch the screen and accept it
  (INSTALL_FAILED_USER_RESTRICTED in the output means the prompt was missed or blocked; enable
  "Install via USB" in Developer options). The adb reverse below is required on physical devices
  because the runner app dials localhost to stream results back.

.PARAMETER Filter
  Optional "dotnet test --filter" expression to run a subset,
  e.g. -Filter "FullyQualifiedName~AndroidFileSystemTests".

.PARAMETER Device
  Optional adb serial to target when several devices are attached.
#>
[CmdletBinding()]
param(
  [string]$Filter,
  [string]$Device
)

$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot

# The TCP port the DeviceRunners CLI listens on (its DeviceRunnersPort default).
$runnerPort = 16384

function Find-Adb {
  foreach ($candidate in @(
      $env:ANDROID_HOME,
      $env:ANDROID_SDK_ROOT,
      'C:\Program Files (x86)\Android\android-sdk')) {
    if ($candidate) {
      $adb = Join-Path $candidate 'platform-tools\adb.exe'
      if (Test-Path $adb) { return $adb }
    }
  }
  $onPath = Get-Command adb -ErrorAction SilentlyContinue
  if ($onPath) { return $onPath.Source }
  throw 'adb.exe not found: set ANDROID_HOME or install the Android SDK platform-tools.'
}

$adb = Find-Adb
$adbArgs = @()
if ($Device) { $adbArgs = @('-s', $Device) }

$devices = & $adb devices | Select-String -Pattern "`tdevice$"
if (-not $devices) {
  throw 'No Android device attached (adb devices is empty). Connect the phone, unlock it and enable USB debugging.'
}
Write-Host "Device(s): $($devices -join '; ')" -ForegroundColor Cyan

& $adb @adbArgs reverse "tcp:$runnerPort" "tcp:$runnerPort"

Write-Host "`n=== GT4.UI.App.DeviceTests (net10.0-android) ===" -ForegroundColor Cyan
Write-Host 'Accept the install prompt on the phone when it appears.' -ForegroundColor Yellow

$testArgs = @(
  'test', (Join-Path $repoRoot 'Tests\GT4.UI.App.DeviceTests\GT4.UI.App.DeviceTests.csproj'),
  '--configuration', 'Release',
  '--framework', 'net10.0-android',
  '-nr:false'
)
if ($Filter) { $testArgs += @('--filter', $Filter) }
if ($Device) { $testArgs += "-p:DeviceRunnersDevice=$Device" }

dotnet @testArgs
exit $LASTEXITCODE
