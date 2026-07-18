<#
.SYNOPSIS
  Builds, deploys and runs GT4.UI.App.DeviceTests on the attached Android device.

  The run has three phases:
    1. Build the test APK.
    2. Deploy + run via "dotnet test" - the script pauses first so you can get ready to accept
       the install prompt on the phone (INSTALL_FAILED_USER_RESTRICTED in the output means the
       prompt was missed or blocked; enable "Install via USB" in Developer options).
    3. If the run fails (device tests can be flaky and sometimes never finish), the script offers
       to relaunch the already-installed app - no rebuild, no redeploy - until it passes or you
       give up: Enter repeats, Esc stops.

  The adb reverse below is required on physical devices because the runner app dials localhost
  to stream results back.

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
$project = Join-Path $repoRoot 'Tests\GT4.UI.App.DeviceTests\GT4.UI.App.DeviceTests.csproj'
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
  $arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
  $cli = Join-Path $env:USERPROFILE ".nuget\packages\devicerunners.testing.targets\0.1.0-preview.12\tools\win-$arch\DeviceRunners.Cli.exe"
  if (-not (Test-Path $cli)) { throw "DeviceRunners CLI not found at $cli (restore the solution first)." }
  return $cli
}

# Returns $true for Enter, $false for Esc; any other key is ignored.
function Wait-EnterOrEsc {
  while ($true) {
    $key = [Console]::ReadKey($true)
    if ($key.Key -eq [ConsoleKey]::Enter) { return $true }
    if ($key.Key -eq [ConsoleKey]::Escape) { return $false }
  }
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

if ($interactive) {
  Write-Host "`nReady to deploy. After pressing Enter, watch the phone and accept the install prompt." -ForegroundColor Yellow
  Write-Host 'Press Enter to continue...' -ForegroundColor Yellow
  while ([Console]::ReadKey($true).Key -ne [ConsoleKey]::Enter) { }
}

Write-Host "`n=== Deploying and running GT4.UI.App.DeviceTests ===" -ForegroundColor Cyan
$testArgs = @(
  'test', $project,
  '--configuration', 'Release',
  '--framework', 'net10.0-android',
  '-nr:false'
)
if ($Filter) { $testArgs += @('--filter', $Filter) }
if ($Device) { $testArgs += "-p:DeviceRunnersDevice=$Device" }
dotnet @testArgs

# Relaunch the installed app without rebuilding/redeploying via the DeviceRunners CLI directly -
# the same command "dotnet test" runs after its Install step.
while ($LASTEXITCODE -ne 0 -and $interactive) {
  Write-Host "`nThe run failed (device tests can be flaky). Press Enter to repeat it without build/deploy, Esc to stop." -ForegroundColor Yellow
  if (-not (Wait-EnterOrEsc)) { break }

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

  Write-Host "`n=== Relaunching the installed test app ===" -ForegroundColor Cyan
  $cli = Find-DeviceRunnersCli
  & $cli @cliArgs
}

exit $LASTEXITCODE
