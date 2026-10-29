param([string]$Executable='src/MTGDeckbuilder.Desktop/bin/Release/net10.0/MTGDeckbuilder.Desktop.exe', [string]$ReportPrefix='phase15')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$exe=[IO.Path]::GetFullPath((Join-Path $root $Executable))
if(Get-Process MTGDeckbuilder.Desktop -ErrorAction SilentlyContinue){throw 'Close existing desktop instances before verification.'}
$previous=$env:MTGDECKBUILDER_DATA_DIR
try {
 foreach($mode in @('filled','user')) {
  $directory=if($mode -eq 'filled'){Join-Path $root '.local/phase9-smoke-data-final'}else{Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'MTGDeckbuilder'}
  $env:MTGDECKBUILDER_DATA_DIR=$directory
  $log=Join-Path $directory ('logs/app-'+(Get-Date -Format yyyyMMdd)+'.log')
  $oldLines=if(Test-Path $log){@(Get-Content $log).Count}else{0}
  $p=Start-Process -FilePath $exe -WorkingDirectory $root -WindowStyle Hidden -PassThru
  $deadline=[DateTimeOffset]::UtcNow.AddSeconds(30)
  do {
   Start-Sleep -Milliseconds 250
   $p.Refresh()
   $lines=if(Test-Path $log){@(Get-Content $log | Select-Object -Skip $oldLines)}else{@()}
   $ready=@($lines | Where-Object {$_ -match 'Database initialized with migrations'}).Count -gt 0
  } while(-not $p.HasExited -and (-not $ready -or $p.MainWindowHandle -eq 0) -and [DateTimeOffset]::UtcNow -lt $deadline)
  if($p.HasExited -or -not $ready -or -not $p.Responding){throw "Startup failed: $mode"}
  # Initialization includes schema creation; let the bound physical workspace finish and image load settle.
  Start-Sleep -Seconds 3
  $p.Refresh(); $lines=@(Get-Content $log | Select-Object -Skip $oldLines)
  if(@($lines|Where-Object {$_ -match '\[(ERR|FTL)\]'}).Count -gt 0){throw "UI error: $mode"}
  $report=[ordered]@{CheckedUtc=[DateTimeOffset]::UtcNow;Mode=$mode;ProcessId=$p.Id;Responding=$p.Responding;Title=$p.MainWindowTitle;DataDirectory=$directory;Version=(Get-Item $exe).VersionInfo.ProductVersion;Log=$lines}
  $report.ClosedNormally=$p.CloseMainWindow(); if(-not $p.WaitForExit(10000)){throw 'Desktop did not close normally.'}
  $report | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $root "artifacts/$ReportPrefix-$mode-native.json") -Encoding utf8
 }
} finally {$env:MTGDECKBUILDER_DATA_DIR=$previous}
