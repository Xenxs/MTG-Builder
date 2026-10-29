param(
    [Parameter(Mandatory)][string]$SourcePath,
    [Parameter(Mandatory)][string]$OutputPath
)
$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath($SourcePath)
$output = [IO.Path]::GetFullPath($OutputPath)
if ($source -eq $output -or (Test-Path -LiteralPath $output)) { throw 'Kies een nieuwe uitvoernaam; bestaande bestanden worden niet overschreven.' }
if ((Get-Item -LiteralPath $source).Length -gt 16MB) { throw 'Het bronbestand is groter dan 16 MiB.' }
$document = Get-Content -LiteralPath $source -Raw -Encoding utf8 | ConvertFrom-Json
if (-not $document.cards -or $document.cards -isnot [Array]) { throw 'Verwacht een Tapps-JSON met een niet-lege cards-lijst.' }
$cards = @()
$seen = [Collections.Generic.HashSet[long]]::new()
$number = 0
[long]$total = 0
foreach ($row in $document.cards) {
    $number++
    # Aggregated exports cannot reveal how a total was split across printings.
    $ids = @($row.arena_ids)
    if ($ids.Count -ne 1) { throw "Regel $number ($($row.name)) bevat meerdere of geen Arena-ID's. Geen bestand gemaakt; exacte aantallen per uitgave zijn nodig." }
    $id = $ids[0]
    $count = $row.count
    if (($id -isnot [int] -and $id -isnot [long]) -or $id -le 0 -or $id -gt [int]::MaxValue) { throw "Ongeldige Arena-ID op regel $number." }
    if (($count -isnot [int] -and $count -isnot [long]) -or $count -lt 0 -or $count -gt 9999) { throw "Ongeldig aantal op regel $number." }
    if (-not $seen.Add([long]$id)) { throw "Arena-ID $id komt dubbel voor. Geen aantallen opgeteld." }
    if ([string]::IsNullOrWhiteSpace($row.name)) { throw "Kaartnaam ontbreekt op regel $number." }
    $cards += [ordered]@{ arenaId = [int]$id; name = [string]$row.name; quantity = [int]$count }
    $total += $count
}
if ($cards.Count -gt 50000) { throw 'Meer dan 50.000 regels.' }
if ($null -eq $document.total_unique -or $null -eq $document.total_cards) { throw 'Tapps-totaaltellers ontbreken; gebruik de JSON-export.' }
if ($document.total_unique -ne $cards.Count -or $document.total_cards -ne $total) { throw 'De totaaltellers kloppen niet. Controleer de bronexport.' }
$json = [ordered]@{ schemaVersion = 1; cards = @($cards) } | ConvertTo-Json -Depth 5
if ([Text.Encoding]::UTF8.GetByteCount($json) -gt 16MB) { throw 'Het resultaat is groter dan 16 MiB.' }
$temporary = $output + '.' + [guid]::NewGuid().ToString('N') + '.tmp'
try {
    [IO.File]::WriteAllText($temporary, $json, [Text.UTF8Encoding]::new($false))
    [IO.File]::Move($temporary, $output)
} finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary } }
Write-Output "$($cards.Count) regels omgezet naar $output. Controleer alle koppelingen in Collectie-import; Arena-ID's kunnen meerdere printings aanwijzen."
