param([Parameter(Mandatory)][string]$Predictions,[Parameter(Mandatory)][string]$Truth,
      [Parameter(Mandatory)][string]$Output,[string]$Interpretation='Development regression; not unseen or independent.',[switch]$IncludeOpponentDeath)
$ErrorActionPreference='Stop'
if(Test-Path $Output){throw 'Preserve existing evaluation evidence; choose a fresh output.'}
$prediction=Get-Content $Predictions -Raw|ConvertFrom-Json
$truthRows=@(Get-Content $Truth -Raw|ConvertFrom-Json)
if($prediction.Cards.Count -ne $truthRows.Count -or @($truthRows.OracleId|Select-Object -Unique).Count -ne $truthRows.Count){throw 'Mismatched or duplicate reference cards.'}
$families=@('equipment','tokens','graveyard','sacrifice','domain','exalted','draw')
if($IncludeOpponentDeath){$families+=@('opponent-death')}
$tp=0;$fp=0;$miss=0;$differences=@()
foreach($expected in $truthRows){
 $actual=@($prediction.Cards|Where-Object OracleId -eq $expected.OracleId)
 if($actual.Count -ne 1){throw 'Missing or duplicated prediction.'}
 $positive=@($actual[0].Themes|Where-Object {$_ -in $families})
 foreach($label in $positive){if($label -in $expected.ExpectedThemes){$tp++}else{$fp++;$differences+=@{Name=$expected.Name;Theme=$label;Issue='False positive'}}}
 foreach($label in $expected.ExpectedThemes){if($label -notin $families){throw 'Reference family outside the predefined seven.'};if($label -notin $positive){$miss++;$differences+=@{Name=$expected.Name;Theme=$label;Issue='Missed'}}}
}
@{EvaluatedUtc=[DateTimeOffset]::UtcNow;Engine=$prediction.Version;Cards=$truthRows.Count;TruePositiveLabels=$tp;FalsePositiveLabels=$fp;MissedLabels=$miss;Differences=$differences;Interpretation=$Interpretation;Predictions=$Predictions;Truth=$Truth}|ConvertTo-Json -Depth 6|Set-Content $Output
Write-Output "$($truthRows.Count) cards: $tp recognized, $fp false, $miss missed. $Interpretation"
