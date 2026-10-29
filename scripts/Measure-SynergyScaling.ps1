param([Parameter(Mandatory)][string]$Tool,[Parameter(Mandatory)][string]$Cards,[Parameter(Mandatory)][string]$OutputDirectory,[int[]]$Sizes=@(100,250,500,1000))
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path $OutputDirectory -Force|Out-Null
$toolPath=[IO.Path]::GetFullPath($Tool);$cardPath=[IO.Path]::GetFullPath($Cards)
foreach($size in $Sizes){
 $output=[IO.Path]::GetFullPath((Join-Path $OutputDirectory "size-$size.json"))
 if(Test-Path $output){throw 'Preserve prior measurements.'}
 $p=Start-Process -FilePath 'dotnet' -ArgumentList @(('"'+$toolPath+'"'),'bench',('"'+$cardPath+'"'),('"'+$output+'"'),$size) -WindowStyle Hidden -PassThru -RedirectStandardOutput "$output.stdout" -RedirectStandardError "$output.stderr"
 $watch=[Diagnostics.Stopwatch]::StartNew();$reason=$null;$peak=0L
 do{Start-Sleep -Milliseconds 250;$p.Refresh();if($p.HasExited){break};$peak=[Math]::Max($peak,$p.WorkingSet64);if($watch.Elapsed.TotalSeconds -gt 60){$reason='60-second wall-time budget'}elseif($p.WorkingSet64 -gt 1GB){$reason='1-GiB working-set budget'}}while(!$reason)
 if($reason){Stop-Process -Id $p.Id -Force;$p.WaitForExit();@{Size=$size;Stopped=$true;Reason=$reason;Seconds=$watch.Elapsed.TotalSeconds;ObservedPeakBytes=$peak}|ConvertTo-Json|Set-Content $output;Write-Output "$size stopped: $reason"}
 elseif($p.ExitCode -ne 0){throw "Probe failed: $size; see $output.stderr"}
 else{Get-Content "$output.stdout"}
}
