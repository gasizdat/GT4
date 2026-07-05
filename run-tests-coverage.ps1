<#
.SYNOPSIS
  Runs every test project (GT4.Core.Project.Tests, GT4.Core.Gedcom.Tests, GT4.UI.Utils.Tests,
  GT4.UI.View.Tests, GT4.UI.App.DeviceTests) with code coverage and shows a combined HTML report at
  the end.

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
  --configuration Release --collect "XPlat Code Coverage" --results-directory $coverageDir

Write-Host "`n=== GT4.Core.Gedcom.Tests ===" -ForegroundColor Cyan
dotnet test (Join-Path $repoRoot 'Tests\GT4.Core.Gedcom.Tests\GT4.Core.Gedcom.Tests.csproj') `
  --configuration Release --collect "XPlat Code Coverage" --results-directory $coverageDir

Write-Host "`n=== GT4.UI.Utils.Tests ===" -ForegroundColor Cyan
dotnet test (Join-Path $repoRoot 'Tests\GT4.UI.Utils.Tests\GT4.UI.Utils.Tests.csproj') `
  --configuration Release --collect "XPlat Code Coverage" --results-directory $coverageDir

Write-Host "`n=== GT4.UI.View.Tests ===" -ForegroundColor Cyan
dotnet test (Join-Path $repoRoot 'Tests\GT4.UI.View.Tests\GT4.UI.View.Tests.csproj') `
  --configuration Release --collect "XPlat Code Coverage" --results-directory $coverageDir

# coverlet's in-process collector never sees this project's tests: DeviceRunners launches
# AppWinOnly.exe as a separate process and drives it over TCP. dotnet-coverage attaches across the
# whole process tree instead, so it still captures real numbers for AppWinOnly.dll. CI's own
# DeviceTests step collects none at all (see ci.yml), so expect AppWinOnly's coverage numbers here
# to permanently differ from what CI reports.
Write-Host "`n=== GT4.UI.App.DeviceTests ===" -ForegroundColor Cyan
$deviceCoverageFile = Join-Path $coverageDir 'device-tests.cobertura.xml'
$deviceTestArgs = @(
  'test', (Join-Path $repoRoot 'Tests\GT4.UI.App.DeviceTests\GT4.UI.App.DeviceTests.csproj'),
  '--configuration', 'Release',
  '--framework', 'net10.0-windows10.0.19041.0'
)
dotnet-coverage collect --output $deviceCoverageFile --output-format cobertura -- dotnet @deviceTestArgs

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
