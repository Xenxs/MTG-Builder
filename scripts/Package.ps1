param([string]$IsccPath, [string]$Version = '1.4.0')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Use a three-part numeric version.' }
$root = Split-Path $PSScriptRoot -Parent
if (-not $IsccPath) { $IsccPath = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source }
if (-not $IsccPath -or -not (Test-Path -LiteralPath $IsccPath)) { throw 'Install Inno Setup 7.1 and supply -IsccPath.' }
$release = Join-Path $root "artifacts/release/$Version"
if (Test-Path -LiteralPath $release) { throw 'Release directory already exists; preserve it and choose a new version/output workspace.' }
$publish = Join-Path $release 'portable'
New-Item -ItemType Directory -Path $publish -Force | Out-Null
dotnet publish (Join-Path $root 'src/MTGDeckbuilder.Desktop') -c Release -r win-x64 --self-contained true -o $publish -m:1 -p:UseSharedCompilation=false -p:PublishTrimmed=false -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false "-p:Version=$Version"
if ($LASTEXITCODE -ne 0) { throw 'Self-contained publish failed.' }
if (-not (Test-Path (Join-Path $publish 'coreclr.dll'))) { throw 'Bundled runtime missing.' }
if (Get-ChildItem $publish -Recurse -File | Where-Object { $_.Extension -in @('.db','.log') }) { throw 'User data in release.' }
New-Item -ItemType Directory -Path (Join-Path $publish 'docs') -Force | Out-Null
foreach ($guide in @('RELEASE_GUIDE.md','FYSIEKE_KAARTEN_HANDLEIDING.md','MAGIC_WIZARD_HANDLEIDING.md','SYNERGY_ENGINE.md','SYNERGY_FULL_CATALOG.md','SYNERGY_ACCEPTANCE_PHASE16.md','VALIDATION_PHASE16.md','SYNERGY_ACCEPTANCE_PHASE17.md','CHANGES_PHASE17.md','COMMANDER.md','PHYSICAL_COLLECTION.md','BACKUP_RESTORE.md','VALIDATION_PHASE12.md','VALIDATION_PHASE17.md','ARCHITECTURE.md','DECISIONS.md')) {
    Copy-Item -LiteralPath (Join-Path $root "docs/$guide") -Destination (Join-Path $publish "docs/$guide")
}
New-Item -ItemType Directory -Path (Join-Path $publish 'examples') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'examples/manabox-conflux.csv') -Destination (Join-Path $publish 'examples/manabox-conflux.csv')
Copy-Item -LiteralPath (Join-Path $root 'examples/manabox-final-fantasy-starter.csv') -Destination (Join-Path $publish 'examples/manabox-final-fantasy-starter.csv')
Copy-Item -LiteralPath (Join-Path $root 'examples/manabox-physical-starters.csv') -Destination (Join-Path $publish 'examples/manabox-physical-starters.csv')
$readme=Get-Content -LiteralPath (Join-Path $root 'README.md') -Raw
$readme=($readme -split '## Ontwikkelen en controleren')[0]
$readme=[regex]::Replace($readme,'Gebruik \[de installer\]\([^\r\n]+\),','Gebruik de installer naast dit pakket,')
$readme | Set-Content -LiteralPath (Join-Path $publish 'README.md') -Encoding utf8
"# MTG Smart Deckbuilder $Version`n`nStart MTGDeckbuilder.Desktop.exe. Lees [installatiegids](docs/RELEASE_GUIDE.md), [synergie en grenzen](docs/SYNERGY_ENGINE.md) en [kaarten scannen en importeren](docs/FYSIEKE_KAARTEN_HANDLEIDING.md). De runtime is inbegrepen; de release is ongetekend." | Set-Content -LiteralPath (Join-Path $publish 'START-HIER.md') -Encoding utf8
& $IsccPath "/DAppVersion=$Version" "/DPublishDir=$publish" "/DReleaseDir=$release" (Join-Path $root 'installer/MTGDeckbuilder.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$zip = Join-Path $release "MTGDeckbuilder-$Version-win-x64-portable.zip"
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip -CompressionLevel Optimal
$manifest = Get-ChildItem -LiteralPath $release -File | Where-Object Extension -in @('.exe','.zip') | Sort-Object Name | ForEach-Object {
    [pscustomobject]@{ Name=$_.Name; Bytes=$_.Length; Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash; Signed=$false }
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $release 'release-manifest.json') -Encoding utf8
$manifest | ForEach-Object { $_.Sha256 + '  ' + $_.Name } | Set-Content -LiteralPath (Join-Path $release 'SHA256SUMS.txt') -Encoding ascii
$manifest

