<#
.SYNOPSIS
  Builds, deploys and runs GT4.UI.App.DeviceTests on the attached Android device.

  The run has three phases:
    1. Build the test APK - all the slow work (ILLink/AOT, packaging, signing) happens here.
    2. Deploy: after an Enter-gated pause (so you are ready at the phone), a plain
       "adb install -r" of the signed APK - seconds, not minutes. Accept the install prompt on
       the phone (INSTALL_FAILED_USER_RESTRICTED means it was missed or blocked; enable
       "Install via USB" in Developer options).
    3. Run: the DeviceRunners CLI launches the installed app and listens for results over TCP.
       If the run fails (device tests can be flaky and sometimes never finish), the script offers
       to relaunch - no rebuild, no redeploy: Enter repeats, Esc stops.

  The adb reverse below is required on physical devices because the runner app dials localhost
  to stream results back.

.PARAMETER Filter
  Optional "dotnet test --filter" expression to run a subset,
  e.g. -Filter "FullyQualifiedName~AndroidFileSystemTests". Baked into the APK at build time.

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
$project = Join-Path $repoRoot 'Tests\GT4.UI.App.DeviceTests\GT4.UI.App.DeviceTests.csproj'
$apk = Join-Path $repoRoot 'Tests\GT4.UI.App.DeviceTests\bin\Release\net10.0-android\com.gasizdat.gt4.devicetests-Signed.apk'
$resultsDir = Join-Path $repoRoot 'Tests\GT4.UI.App.DeviceTests\test-results'
$packageId = 'com.gasizdat.gt4.devicetests'

# DeviceRunners defaults (see DeviceRunners.Testing.Targets.props).
$runnerPort = 16384
$connectionTimeout = 120
$dataTimeout = 30

$interactive = -not [Console]::IsInputRedirected

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

function Find-DeviceRunnersCli {
  # PROCESSOR_ARCHITECTURE instead of RuntimeInformation.OSArchitecture: the latter is null on
  # some Windows PowerShell hosts.
  $arch = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }
  $pattern = Join-Path $env:USERPROFILE ".nuget\packages\devicerunners.testing.targets\*\tools\win-$arch\DeviceRunners.Cli.exe"
  $cli = Get-Item $pattern -ErrorAction SilentlyContinue | Sort-Object FullName | Select-Object -Last 1
  if (-not $cli) { throw "DeviceRunners CLI not found under $pattern (restore the solution first)." }
  return $cli.FullName
}

# Returns $true for Enter, $false for Esc; any other key is ignored.
function Wait-EnterOrEsc {
  while ($true) {
    $key = [Console]::ReadKey($true)
    if ($key.Key -eq [ConsoleKey]::Enter) { return $true }
    if ($key.Key -eq [ConsoleKey]::Escape) { return $false }
  }
}

function Invoke-TestRun([string]$cliPath) {
  $cliArgs = @(
    'android', 'test',
    '--package', $packageId,
    '--results-directory', $resultsDir,
    '--port', $runnerPort,
    '--connection-timeout', $connectionTimeout,
    '--data-timeout', $dataTimeout
  )
  if ($Filter) { $cliArgs += @('--filter', $Filter) }
  if ($Device) { $cliArgs += @('--device', $Device) }

  if (-not (Test-Path $resultsDir)) { New-Item -ItemType Directory -Path $resultsDir | Out-Null }
  & $cliPath @cliArgs
}

$adb = Find-Adb
$cli = Find-DeviceRunnersCli
$adbArgs = @()
if ($Device) { $adbArgs = @('-s', $Device) }

$devices = & $adb devices | Select-String -Pattern "`tdevice$"
if (-not $devices) {
  throw 'No Android device attached (adb devices is empty). Connect the phone, unlock it and enable USB debugging.'
}
Write-Host "Device(s): $($devices -join '; ')" -ForegroundColor Cyan

& $adb @adbArgs reverse "tcp:$runnerPort" "tcp:$runnerPort"
if ($LASTEXITCODE -ne 0) {
  throw "adb reverse tcp:$runnerPort failed - without it the app cannot reach the host's TCP listener."
}

Write-Host "`n=== Building the test APK (net10.0-android) ===" -ForegroundColor Cyan
$buildArgs = @(
  'build', $project,
  '--configuration', 'Release',
  '--framework', 'net10.0-android',
  '-nr:false',
  '-p:DeviceRunnersTestRun=true'
)
if ($Filter) { $buildArgs += "-p:VSTestTestCaseFilter=$Filter" }
dotnet @buildArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if (-not (Test-Path $apk)) { throw "Signed APK not found at $apk." }

if ($interactive) {
  Write-Host "`nReady to deploy. After pressing Enter, watch the phone and accept the install prompt." -ForegroundColor Yellow
  Write-Host 'Press Enter to continue...' -ForegroundColor Yellow
  while ([Console]::ReadKey($true).Key -ne [ConsoleKey]::Enter) { }
}

Write-Host "`n=== Installing $([System.IO.Path]::GetFileName($apk)) ===" -ForegroundColor Cyan
& $adb @adbArgs install -r $apk
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n=== Running GT4.UI.App.DeviceTests ===" -ForegroundColor Cyan
Invoke-TestRun $cli

while ($LASTEXITCODE -ne 0 -and $interactive) {
  Write-Host "`nThe run failed (device tests can be flaky). Press Enter to repeat it without build/deploy, Esc to stop." -ForegroundColor Yellow
  if (-not (Wait-EnterOrEsc)) { break }

  Write-Host "`n=== Relaunching the installed test app ===" -ForegroundColor Cyan
  Invoke-TestRun $cli
}

exit $LASTEXITCODE
