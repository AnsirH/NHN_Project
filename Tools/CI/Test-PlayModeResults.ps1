param([Parameter(Mandatory = $true)][string]$ResultsPath)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
[xml]$results = Get-Content -LiteralPath $ResultsPath -Raw
$run = $results.DocumentElement
if ($run.Name -ne 'test-run' -or [int]$run.GetAttribute('total') -eq 0 -or
    $run.GetAttribute('result') -notin @('Passed', 'Failed', 'Failed(Child)') -or
    -not $run.GetAttribute('end-time') -or -not $run.GetAttribute('duration')) {
    throw 'PlayMode did not produce a completed, non-empty NUnit test run.'
}
$cases = @($results.SelectNodes('//test-case'))
if ($cases.Count -ne [int]$run.GetAttribute('total') -or [int]$run.GetAttribute('inconclusive') -ne 0 -or
    $cases.Count -ne ([int]$run.GetAttribute('passed') + [int]$run.GetAttribute('failed') + [int]$run.GetAttribute('skipped'))) {
    throw 'PlayMode test run is incomplete or has inconsistent totals.'
}
$failures = @($results.SelectNodes('//test-case[@result="Failed"]'))
if ($failures.Count -ne [int]$run.GetAttribute('failed')) {
    throw 'PlayMode run contains an unexpected suite/setup failure.'
}
if ($failures.Count -or $run.GetAttribute('result') -ne 'Passed') {
    throw ('PlayMode failures: ' + (($failures | ForEach-Object { $_.GetAttribute('fullname') }) -join ', '))
}
Write-Host "PlayMode: $($run.GetAttribute('passed')) passed, 0 failures, $($run.GetAttribute('skipped')) skipped."
