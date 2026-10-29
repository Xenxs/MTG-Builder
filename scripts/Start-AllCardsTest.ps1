param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$directory = [IO.Path]::GetFullPath((Join-Path $root '.local/phase17-all-cards'))
$marker = Join-Path $directory 'FULL-CATALOG-SIMULATION.json'
$executable = Join-Path $root 'artifacts/release/1.4.0/portable/MTGDeckbuilder.Desktop.exe'
if (-not (Test-Path -LiteralPath $marker) -or -not (Test-Path -LiteralPath $executable)) {
    throw 'De afzonderlijke alle-kaartenproef en de portable v1.4.0 moeten in deze werkmap aanwezig zijn.'
}
$snapshot = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
if ($snapshot.Summary.Printings -ne 33727 -or -not $snapshot.Summary.DemoActive) {
    throw 'Dit is niet de voorbereide gesimuleerde testcollectie.'
}
if ([DateTimeOffset]::UtcNow - [DateTimeOffset]$snapshot.RecordedUtc -ge [TimeSpan]::FromDays(1)) {
    throw 'De bron van deze tijdelijke proef is ouder dan 24 uur. Vernieuw eerst de bulkproef in een nieuwe testmap; dit voorkomt duizenden afzonderlijke kaartverzoeken. De opgeslagen rapporten blijven bruikbaar. De gewone applicatie gebruikt je eigen gegevens.'
}
$previous = $env:MTGDECKBUILDER_DATA_DIR
try {
    $env:MTGDECKBUILDER_DATA_DIR = $directory
    $process = Start-Process -FilePath $executable -WorkingDirectory $root -PassThru
    Write-Output "Alle-kaartenproef gestart (proces $($process.Id)). SIMULATIE: 33.727 kaarten, geen echte scan. Gewone collectie staat apart."
} finally { $env:MTGDECKBUILDER_DATA_DIR = $previous }
