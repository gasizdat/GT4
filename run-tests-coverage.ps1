<#
.SYNOPSIS
  Runs every test project (GT4.Core.Project.Tests, GT4.Core.Gedcom.Tests, GT4.Tools.RelativesCli.Tests,
  GT4.UI.Utils.Tests, GT4.UI.View.Tests, GT4.UI.App.DeviceTests) with code coverage, then the GEDCOM
  round-trip functional tests, and shows a combined HTML report at the end.

.PARAMETER SkipOpen
  Don't launch the generated report in the default browser.
#>
[CmdletBinding()]
param(
  [switch]$SkipOpen
)

$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot
$coverageDir = Join-Path $repoRoot 'coverage'
$toolsPath = Join-Path $env:USERPROFILE '.dotnet\tools'

# Force English output regardless of OS locale: DOTNET_CLI_UI_LANGUAGE covers the dotnet CLI itself,
# VSLANG (an LCID, 1033 = en-US) covers MSBuild/Roslyn satellite resources.
$env:DOTNET_CLI_UI_LANGUAGE = 'en'
$env:VSLANG = '1033'

if (Test-Path $coverageDir) { Remove-Item $coverageDir -Recurse -Force }
New-Item -ItemType Directory -Path $coverageDir | Out-Null

function Install-GlobalToolIfMissing([string]$toolName) {
  $installed = dotnet tool list --global | Select-String -Pattern "^$toolName\b" -Quiet
  if (-not $installed) {
    Write-Host "Installing $toolName..." -ForegroundColor Yellow
    dotnet tool install --global $toolName | Out-Null
  }
}

Install-GlobalToolIfMissing 'dotnet-coverage'
Install-GlobalToolIfMissing 'dotnet-reportgenerator-globaltool'
if ($env:PATH -notlike "*$toolsPath*") { $env:PATH = "$env:PATH;$toolsPath" }

Write-Host "`n=== GT4.Core.Project.Tests ===" -ForegroundColor Cyan
dotnet test (Join-Path $repoRoot 'Tests\GT4.Core.Project.Tests\GT4.Core.Project.Tests.csproj') `
  --configuration Release --collect "XPlat Code Coverage" --results-directory $coverageDir `
  --logger "console;verbosity=detailed"

Write-Host "`n=== GT4.Core.Gedcom.Tests ===" -ForegroundColor Cyan
dotnet test (Join-Path $repoRoot 'Tests\GT4.Core.Gedcom.Tests\GT4.Core.Gedcom.Tests.csproj') `
  --configuration Release --collect "XPlat Code Coverage" --results-directory $coverageDir `
  --logger "console;verbosity=detailed"

Write-Host "`n=== GT4.Tools.RelativesCli.Tests ===" -ForegroundColor Cyan
dotnet test (Join-Path $repoRoot 'Tests\GT4.Tools.RelativesCli.Tests\GT4.Tools.RelativesCli.Tests.csproj') `
  --configuration Release --collect "XPlat Code Coverage" --results-directory $coverageDir `
  --logger "console;verbosity=detailed"

Write-Host "`n=== GT4.UI.Utils.Tests ===" -ForegroundColor Cyan
dotnet test (Join-Path $repoRoot 'Tests\GT4.UI.Utils.Tests\GT4.UI.Utils.Tests.csproj') `
  --configuration Release --collect "XPlat Code Coverage" --results-directory $coverageDir `
  --logger "console;verbosity=detailed"

Write-Host "`n=== GT4.UI.View.Tests ===" -ForegroundColor Cyan
dotnet test (Join-Path $repoRoot 'Tests\GT4.UI.View.Tests\GT4.UI.View.Tests.csproj') `
  --configuration Release --collect "XPlat Code Coverage" --results-directory $coverageDir `
  --logger "console;verbosity=detailed"

# coverlet's in-process collector never sees this project's tests: DeviceRunners launches
# AppWinOnly.exe as a separate process and drives it over TCP. dotnet-coverage attaches across the
# whole process tree instead, so it still captures real numbers for AppWinOnly.dll. CI's own
# DeviceTests step collects none at all (see ci.yml), so expect AppWinOnly's coverage numbers here
# to permanently differ from what CI reports.
Write-Host "`n=== GT4.UI.App.DeviceTests ===" -ForegroundColor Cyan
$deviceCoverageFile = Join-Path $coverageDir 'device-tests.cobertura.xml'
$deviceTrxFile = Join-Path $coverageDir 'device-tests.trx'
$deviceTestArgs = @(
  'test', (Join-Path $repoRoot 'Tests\GT4.UI.App.DeviceTests\GT4.UI.App.DeviceTests.csproj'),
  '--configuration', 'Release',
  '--framework', 'net10.0-windows10.0.19041.0',
  '--logger', 'trx;LogFileName=device-tests.trx',
  '--results-directory', $coverageDir
)

# Retried the same way as ci.yml's test-device job: the DeviceRunners CLI can exit non-zero from a
# native WinUI teardown race, sometimes after every test already passed, and one attempt's output
# cannot tell that apart from a crash that left tests unexecuted - so the whole attempt is retried,
# a genuine bug being the one that reproduces. A TRX recording a failed test is a real failure and
# is never retried past.
# Only the exit code distinguishes a failed run: each attempt overwrites the coverage file, and a
# failing one still leaves a valid but empty cobertura behind that the report below merges happily.
$maxAttempts = 3
for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
  Write-Host "--- Device test attempt $attempt of $maxAttempts ---" -ForegroundColor Cyan
  if (Test-Path $deviceTrxFile) { Remove-Item $deviceTrxFile -Force }

  dotnet-coverage collect --output $deviceCoverageFile --output-format cobertura -- dotnet @deviceTestArgs
  $deviceTestsExit = $LASTEXITCODE

  if (Test-Path $deviceTrxFile) {
    [xml]$trx = Get-Content $deviceTrxFile
    $counters = $trx.TestRun.ResultSummary.Counters
    Write-Host "Attempt $attempt`: $($counters.total) total, $($counters.failed) failed (exit code $deviceTestsExit)."
    if ([int]$counters.failed -gt 0) {
      Write-Host "Real test failure recorded -- not retrying." -ForegroundColor Red
      break
    }
  } else {
    Write-Host "Attempt $attempt`: no TRX produced (exit code $deviceTestsExit)."
  }

  if ($deviceTestsExit -eq 0) { break }

  if ($attempt -lt $maxAttempts) {
    Write-Host "Attempt $attempt exited non-zero with no failed test recorded -- retrying." -ForegroundColor Yellow
  }
}

# Not collected into the coverage report: this drives the built CLI over real sample files as separate
# processes, and it skips itself when the gedcom-samples repository is not checked out next to GT4.
# $ErrorActionPreference = 'Stop' does not trip on a script's non-zero exit, so check it explicitly.
Write-Host "`n=== GEDCOM round-trip (functional) ===" -ForegroundColor Cyan
& (Join-Path $repoRoot 'Tests\Functional\run-gedcom-roundtrip.ps1')
$roundTripExit = $LASTEXITCODE

Write-Host "`n=== Generating coverage report ===" -ForegroundColor Cyan
$reportDir = Join-Path $coverageDir 'report'
reportgenerator "-reports:$coverageDir\**\coverage.cobertura.xml;$deviceCoverageFile" `
  "-targetdir:$reportDir" "-reporttypes:Html;TextSummary" "-assemblyfilters:+GT4*;+AppWinOnly;-*Tests*"

$summaryFile = Join-Path $reportDir 'Summary.txt'
if (Test-Path $summaryFile) {
  Write-Host "`n=== Coverage summary ===" -ForegroundColor Green
  Get-Content $summaryFile
}

$indexFile = Join-Path $reportDir 'index.html'
if (-not $SkipOpen -and (Test-Path $indexFile)) {
  Start-Process $indexFile
}

if ($deviceTestsExit -ne 0) {
  Write-Host "`nGT4.UI.App.DeviceTests failed." -ForegroundColor Red
  exit $deviceTestsExit
}

if ($roundTripExit -ne 0) {
  Write-Host "`nGEDCOM round-trip functional tests failed." -ForegroundColor Red
  exit $roundTripExit
}
