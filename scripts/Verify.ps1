param([switch]$SkipRestore, [switch]$Screenshots, [string]$ReportPrefix = 'phase17', [string]$ScreenshotFolder = 'artifacts/ui/phase17')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$previousScreenshotDirectory = $env:MTGDECKBUILDER_SCREENSHOTS
Push-Location $projectRoot
try {
    New-Item -ItemType Directory -Path 'artifacts' -Force | Out-Null
    if (-not $SkipRestore) {
        dotnet restore MTGDeckbuilder.sln
        if ($LASTEXITCODE -ne 0) { throw 'NuGet restore failed.' }
    }
    # Single-node compilation also works in environments with restricted IPC.
    dotnet build MTGDeckbuilder.sln -c Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal 2>&1 | Tee-Object artifacts/build.log
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    if ($Screenshots) { $env:MTGDECKBUILDER_SCREENSHOTS = Join-Path $projectRoot $ScreenshotFolder }
    dotnet test MTGDeckbuilder.sln -c Release --no-build --no-restore -m:1 --logger "trx;LogFilePrefix=$ReportPrefix" --results-directory artifacts/test-results
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
} finally {
    $env:MTGDECKBUILDER_SCREENSHOTS = $previousScreenshotDirectory
    Pop-Location
}
