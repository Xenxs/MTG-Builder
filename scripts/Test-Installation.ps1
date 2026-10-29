param([Parameter(Mandatory)][string]$Setup, [Parameter(Mandatory)][string]$BaselineSetup,
    [Parameter(Mandatory)][string]$FixtureDatabase, [ValidatePattern('^[a-z0-9-]+$')][string]$TestName='phase16-install',
    [int]$BaselineSchema=9,[int]$CurrentSchema=9,[int]$ExpectedMigrationBackups=0)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$install=[IO.Path]::GetFullPath((Join-Path $root ".local/$TestName"))
$data=[IO.Path]::GetFullPath((Join-Path $root ".local/$TestName-data"))
if(-not $install.StartsWith([IO.Path]::GetFullPath((Join-Path $root '.local/')),[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe install target.'}
if((Test-Path $install) -or (Test-Path $data)){throw 'Use fresh test directories.'}
New-Item -ItemType Directory -Path $data -Force | Out-Null
$old=[IO.Path]::GetFullPath($FixtureDatabase)
$registration=Get-ItemProperty 'HKCU:/Software/Microsoft/Windows/CurrentVersion/Uninstall/{B3161121-CA76-4F5B-839F-E095FAE460A7}_is1' -ErrorAction SilentlyContinue
if($registration -and -not $registration.InstallLocation.StartsWith([IO.Path]::GetFullPath((Join-Path $root '.local/')),[StringComparison]::OrdinalIgnoreCase)){throw 'An installation outside the test workspace exists; do not replace it.'}
if((Test-Path ($old+'-wal')) -and (Get-Item ($old+'-wal')).Length -gt 0){throw 'Source has pending WAL; use SQLite backup first.'}
Copy-Item -LiteralPath $old -Destination (Join-Path $data 'deckbuilder.db')
$helperProject=Join-Path $root 'scripts/DatabaseAudit/DatabaseAudit.csproj'
dotnet build $helperProject -c Release -m:1 -p:UseSharedCompilation=false -v:minimal
if($LASTEXITCODE -ne 0){throw 'Audit helper build failed.'}
$helper=Join-Path $root 'scripts/DatabaseAudit/bin/Release/net10.0/DatabaseAudit.dll'
function Audit([string]$name){dotnet $helper (Join-Path $root "artifacts/$TestName-$name.json") $data | Out-Null;if($LASTEXITCODE -ne 0){throw 'Audit failed.'}; Get-Content (Join-Path $root "artifacts/$TestName-$name.json") -Raw | ConvertFrom-Json}
function Install([string]$exe,[string]$label){$p=Start-Process -FilePath (Resolve-Path $exe) -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-',('/DIR="'+$install+'"'),('/LOG="'+(Join-Path $root "artifacts/$TestName-$label.log")+'"')) -WindowStyle Hidden -PassThru -Wait;if($p.ExitCode -ne 0){throw "Installer failed $label : $($p.ExitCode)"}}
$previous=$env:MTGDECKBUILDER_DATA_DIR
$evidence=@()
try {
 $env:MTGDECKBUILDER_DATA_DIR=$data
 $before=Audit 'before'
 foreach($step in @('baseline','upgrade','reinstall')) {
  Install $(if($step -eq 'baseline'){$BaselineSetup}else{$Setup}) $step
  $exe=Join-Path $install 'MTGDeckbuilder.Desktop.exe'
  $log=Join-Path $data ('logs/app-'+(Get-Date -Format yyyyMMdd)+'.log')
  $lines=if(Test-Path $log){@(Get-Content $log).Count}else{0}
  $p=Start-Process -FilePath $exe -WorkingDirectory $install -WindowStyle Hidden -PassThru
  $deadline=[DateTimeOffset]::UtcNow.AddSeconds(30)
  do{Start-Sleep -Milliseconds 250;$p.Refresh();$new=if(Test-Path $log){@(Get-Content $log | Select-Object -Skip $lines)}else{@()};$ready=@($new | Where-Object {$_ -match 'Database initialized with migrations'}).Count -gt 0}while(-not $p.HasExited -and (-not $ready -or $p.MainWindowHandle -eq 0) -and [DateTimeOffset]::UtcNow -lt $deadline)
  if($p.HasExited -or -not $ready -or -not $p.Responding){throw "Installed app failed: $step"}
  if($step -ne 'baseline') {
   $workspaceDeadline=[DateTimeOffset]::UtcNow.AddSeconds(30)
   do {
    Start-Sleep -Milliseconds 250
    $workspace=Audit "$step-workspace"
    $workspaceReady=$workspace.UserData.PaperHoldings.Rows -eq 118 -and $workspace.UserData.PaperDecks.Rows -eq 4
    $p.Refresh()
   }while(-not $p.HasExited -and -not $workspaceReady -and [DateTimeOffset]::UtcNow -lt $workspaceDeadline)
   if($p.HasExited -or -not $workspaceReady){throw "Physical Conflux workspace did not finish startup: $step"}
  }
  Start-Sleep -Seconds 2
  $new=@(Get-Content $log | Select-Object -Skip $lines)
  if(@($new|Where-Object {$_ -match '\[(ERR|FTL)\]'}).Count -gt 0){throw "Installed UI logged an error: $step"}
  $runtime=@($p.Modules | Where-Object ModuleName -eq 'coreclr.dll' | Select-Object -ExpandProperty FileName)
  if($runtime.Count -ne 1 -or -not $runtime[0].StartsWith($install,[StringComparison]::OrdinalIgnoreCase)){throw 'Bundled runtime not loaded.'}
  $evidence += [pscustomobject]@{Step=$step;ProcessId=$p.Id;Responding=$p.Responding;Runtime=$runtime[0];Version=(Get-Item $exe).VersionInfo.ProductVersion;Log=$new}
  if(-not $p.CloseMainWindow() -or -not $p.WaitForExit(10000)){throw 'Close failed.'}
  $after=Audit $step
  $hashProperty=if($BaselineSchema -eq $CurrentSchema){'Sha256'}else{'RetainedSha256'}
  foreach($table in $before.UserData.PSObject.Properties.Name){if(-not $before.UserData.$table.Exists){continue};if($before.UserData.$table.$hashProperty -ne $after.UserData.$table.$hashProperty){throw "Data changed: $table"}}
  $expectedMigrations=if($step -eq 'baseline'){$BaselineSchema}else{$CurrentSchema}; if($after.Migrations.Count -ne $expectedMigrations){throw 'Migration count mismatch.'}
  $before=$after
  if($step -eq 'upgrade'){
   $uninstaller=Join-Path $install 'unins000.exe'
   $u=Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -WindowStyle Hidden -PassThru -Wait
   if($u.ExitCode -ne 0 -or (Test-Path $exe)){throw 'Uninstall failed.'}
   $retained=Audit 'uninstalled'
   foreach($table in $before.UserData.PSObject.Properties.Name){if($before.UserData.$table.Sha256 -ne $retained.UserData.$table.Sha256){throw 'Uninstall changed data.'}}
   $evidence += [pscustomobject]@{Step='uninstall';ProgramRemoved=$true;AllUserDataRetained=$true}
  }
 }
 $shortcut=Join-Path ([Environment]::GetFolderPath('Programs')) 'MTG Smart Deckbuilder/MTG Smart Deckbuilder.lnk'
 if(-not (Test-Path $shortcut)){throw 'Start menu shortcut missing.'}
 $target=(New-Object -ComObject WScript.Shell).CreateShortcut($shortcut).TargetPath
 if($target -ne (Join-Path $install 'MTGDeckbuilder.Desktop.exe')){throw 'Shortcut target mismatch.'}
 $cleanup=Start-Process -FilePath (Join-Path $install 'unins000.exe') -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -WindowStyle Hidden -PassThru -Wait
 if($cleanup.ExitCode -ne 0 -or (Test-Path (Join-Path $install 'MTGDeckbuilder.Desktop.exe')) -or (Test-Path $shortcut)){throw 'Final test uninstall failed.'}
 $retained=Audit 'final-uninstalled'
 foreach($table in $before.UserData.PSObject.Properties.Name){if($before.UserData.$table.Sha256 -ne $retained.UserData.$table.Sha256){throw 'Final uninstall changed data.'}}
 $backupDirectory=Join-Path $data 'backups';$backupCount=if(Test-Path $backupDirectory){@(Get-ChildItem $backupDirectory -Filter '*.db').Count}else{0}
 if($backupCount -ne $ExpectedMigrationBackups){throw 'Unexpected migration backup count.'}
 $evidence += [pscustomobject]@{Step='final-uninstall';ProgramAndShortcutRemoved=$true;DataAndMigrationBackupRetained=$true}
 [pscustomobject]@{CheckedUtc=[DateTimeOffset]::UtcNow;InstallDirectory=$install;DataDirectory=$data;Shortcut=$shortcut;ShortcutTarget=$target;Steps=$evidence;DataPreserved=$true;BaselineSetup=[IO.Path]::GetFullPath($BaselineSetup);CurrentSetup=[IO.Path]::GetFullPath($Setup);OldSchemaMigratedFrom=$BaselineSchema;CurrentSchema=$CurrentSchema;MigrationBackups=$backupCount} | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $root "artifacts/$TestName-installation.json") -Encoding utf8
}finally{$env:MTGDECKBUILDER_DATA_DIR=$previous}

