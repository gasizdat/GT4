<#
.SYNOPSIS
  Round-trips real GEDCOM files through GT4 (ged -> project -> ged) and checks that nothing the pipeline
  can carry was lost on the way.

.DESCRIPTION
  Two checks per sample, each strict in its own dimension:

    stability  export(import(original)) vs export(import(that)) -- byte-identical. Everything the pipeline
               holds must be held identically on the second pass, whatever the first pass made of it.
    fidelity   original vs its first export, through "RelativesCli compare", which compares what the tags
               say rather than their text (xrefs are renumbered and FAM records regenerated on every
               export, so nothing that identifies a record survives literally). A sample GT4 cannot yet
               carry losslessly is held to an exact baseline count instead of to zero -- see
               -FidelityBaselines.

  Samples live in a separate repository (gedcom-samples) that CI machines do not have: when it is missing
  the script reports a skip and exits 0.

.PARAMETER SamplesRoot
  Directory holding the sample repositories. Defaults to gedcom-samples beside the GT4 checkout.

.PARAMETER Samples
  Sample paths relative to SamplesRoot. Each must exist, or the run fails. UTF-8 files only: the CLI
  reads and writes UTF-8 without consulting the declared charset, so an ANSEL sample would be
  mis-decoded rather than rejected (see issue #123).

.PARAMETER FidelityBaselines
  Samples that do not round-trip cleanly yet: how many differences are known and which issues they are
  tracked under. The count is exact in both directions -- one more is a regression, one fewer means an
  issue has been fixed and the baseline is now stale.

.PARAMETER WorkDir
  Where the intermediate projects and exports go. Defaults to a new temp directory, removed on exit
  unless -KeepArtifacts is given.
#>
[CmdletBinding()]
param(
  [string]$SamplesRoot,
  [string[]]$Samples = @('sample-kennedy\kennedy.ged', 'sample-bourbon\bourbon.ged'),
  [hashtable]$FidelityBaselines = @{
    'sample-bourbon\bourbon.ged' = @{ Differences = 47; Issues = 'issues #172, #173, #175, #176' }
  },
  [string]$WorkDir,
  [switch]$KeepArtifacts
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent

if (-not $SamplesRoot) {
  $SamplesRoot = Join-Path (Split-Path $repoRoot -Parent) 'gedcom-samples'
}

if (-not (Test-Path $SamplesRoot)) {
  Write-Host "SKIP: no GEDCOM samples at $SamplesRoot (they live in a separate repository)." -ForegroundColor Yellow
  exit 0
}

$missing = $Samples | Where-Object { -not (Test-Path (Join-Path $SamplesRoot $_)) }
if ($missing) {
  Write-Host "FAIL: samples missing from $SamplesRoot :" -ForegroundColor Red
  $missing | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
  exit 1
}

$cliProject = Join-Path $repoRoot 'Tools\GT4.Tools.RelativesCli\GT4.Tools.RelativesCli.csproj'
Write-Host "Building the CLI..." -ForegroundColor Cyan
dotnet build $cliProject --configuration Release --nologo --verbosity quiet
if ($LASTEXITCODE -ne 0) {
  Write-Host "FAIL: could not build $cliProject" -ForegroundColor Red
  exit 1
}
$cli = Join-Path $repoRoot 'Tools\GT4.Tools.RelativesCli\bin\Release\net10.0\GT4.Tools.RelativesCli.exe'

if (-not $WorkDir) {
  $WorkDir = Join-Path ([System.IO.Path]::GetTempPath()) "gt4_roundtrip_$([Guid]::NewGuid().ToString('N'))"
}
New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null

# The CLI returns 2 for "the files differ" and 1 for anything that went wrong, so a broken invocation is
# never mistaken for a clean difference report. stderr is deliberately not redirected into $output:
# under $ErrorActionPreference = 'Stop', "2>&1" on a native exe turns its error lines into a terminating
# NativeCommandError, which would kill the run instead of reporting the failed sample.
function Invoke-Cli {
  param([string[]]$CliArgs)
  $output = & $cli @CliArgs
  return @{ ExitCode = $LASTEXITCODE; Output = $output }
}

$results = @()
$failed = $false

foreach ($sample in $Samples) {
  $name = [System.IO.Path]::GetFileNameWithoutExtension($sample)
  $source = Join-Path $SamplesRoot $sample
  Write-Host "`n=== $name ===" -ForegroundColor Cyan

  # Without this, a sample that parses to nothing at all would compare equal to the nothing it exports
  # and report PASS -- a parse regression would look like perfect fidelity.
  $individuals = @(Select-String -Path $source -Pattern '^0 @.+@ INDI').Count
  if ($individuals -eq 0) {
    Write-Host "  no INDI records read from $source." -ForegroundColor Red
    $results += [pscustomobject]@{ Sample = $name; Stability = 'ERROR'; Fidelity = '-' }
    $failed = $true
    continue
  }
  Write-Host "  $individuals individuals in the source." -ForegroundColor DarkGray

  $firstGed = Join-Path $WorkDir "$name.1.ged"
  $secondGed = Join-Path $WorkDir "$name.2.ged"

  $pass1 = Invoke-Cli @('--gedcom', $source, '--out', (Join-Path $WorkDir "$name.1.db"), 'export', $firstGed)
  $pass2 = Invoke-Cli @('--gedcom', $firstGed, '--out', (Join-Path $WorkDir "$name.2.db"), 'export', $secondGed)
  if ($pass1.ExitCode -ne 0 -or $pass2.ExitCode -ne 0) {
    Write-Host "  round-trip failed to run:" -ForegroundColor Red
    $pass1.Output + $pass2.Output | ForEach-Object { Write-Host "    $_" }
    $results += [pscustomobject]@{ Sample = $name; Stability = 'ERROR'; Fidelity = '-' }
    $failed = $true
    continue
  }

  $firstHash = (Get-FileHash $firstGed -Algorithm SHA256).Hash
  $secondHash = (Get-FileHash $secondGed -Algorithm SHA256).Hash
  $stability = 'PASS'
  if ($firstHash -ne $secondHash) {
    $stability = 'FAIL'
    $failed = $true
    Write-Host "  stability: the second export differs from the first." -ForegroundColor Red
    # The comparer only has to explain a failure here; byte equality is the check.
    $explain = Invoke-Cli @('compare', $firstGed, $secondGed)
    $explain.Output | ForEach-Object { Write-Host "    $_" }
  } else {
    Write-Host "  stability: identical on the second pass." -ForegroundColor Green
  }

  $compare = Invoke-Cli @('compare', $source, $firstGed)
  $baseline = $FidelityBaselines[$sample]
  $expected = if ($baseline) { $baseline.Differences } else { 0 }

  if ($compare.ExitCode -notin 0, 2) {
    Write-Host "  fidelity: the comparison itself failed (exit $($compare.ExitCode))." -ForegroundColor Red
    $compare.Output | ForEach-Object { Write-Host "    $_" }
    $results += [pscustomobject]@{ Sample = $name; Stability = $stability; Fidelity = 'ERROR' }
    $failed = $true
    continue
  }

  # The CLI's own tally, rather than a count of output lines: a difference detail can span several lines.
  $tally = $compare.Output | Where-Object { $_ -match '^(\d+) difference\(s\)' } | Select-Object -First 1
  $actual = if ($tally -match '^(\d+)') { [int]$Matches[1] } else { 0 }

  if ($actual -eq $expected -and $expected -eq 0) {
    Write-Host "  fidelity: no differences." -ForegroundColor Green
    $fidelity = 'PASS'
  } elseif ($actual -eq $expected) {
    Write-Host "  fidelity: $actual known differences, tracked in $($baseline.Issues)." -ForegroundColor Yellow
    $fidelity = "BASELINE ($actual)"
  } else {
    if ($actual -gt $expected) {
      Write-Host "  fidelity: $actual differences, $expected expected -- the round-trip lost or changed more than it did." -ForegroundColor Red
    } else {
      Write-Host "  fidelity: $actual differences, $expected expected -- fixed more than the baseline records; lower it." -ForegroundColor Red
    }
    $compare.Output | ForEach-Object { Write-Host "    $_" }
    $fidelity = 'FAIL'
    $failed = $true
  }
  $results += [pscustomobject]@{ Sample = $name; Stability = $stability; Fidelity = $fidelity }
}

Write-Host "`n=== GEDCOM round-trip summary ===" -ForegroundColor Cyan
$results | Format-Table -AutoSize | Out-String | Write-Host

if ($KeepArtifacts -or $failed) {
  Write-Host "Artifacts kept in $WorkDir"
} else {
  Remove-Item $WorkDir -Recurse -Force
}

if ($failed) {
  exit 1
}
exit 0
