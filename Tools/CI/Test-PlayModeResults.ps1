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
$known = @(Get-Content (Join-Path $PSScriptRoot 'KnownPlayModeFailures.txt') | Where-Object { $_.Trim() })
$unexpected = @($failures | Where-Object { $_.GetAttribute('fullname') -notin $known })
if ($unexpected.Count) {
    throw ('New PlayMode failures: ' + (($unexpected | ForEach-Object { $_.GetAttribute('fullname') }) -join ', '))
}
$signatures = Get-Content (Join-Path $PSScriptRoot 'KnownPlayModeSignatures.json') -Raw | ConvertFrom-Json
foreach ($failure in $failures) {
    $signature = @($signatures | Where-Object { $_.name -eq $failure.GetAttribute('fullname') })
    if ($signature.Count -ne 1) { throw 'Missing unique baseline failure signature.' }
    foreach ($fragment in $signature[0].messageContains) {
        if (-not $failure.SelectSingleNode('failure/message').InnerText.Contains($fragment)) {
            throw "Changed baseline failure: $($signature[0].name)"
        }
    }
    if ($signature[0].stackContains -and -not $failure.SelectSingleNode('failure/stack-trace').InnerText.Contains($signature[0].stackContains)) {
        throw "Changed baseline failure location: $($signature[0].name)"
    }
}
if ($failures.Count) {
    Write-Warning "$($failures.Count) known baseline PlayMode failures remain. See Logs/playmode.xml and Tools/CI/README.md."
}
Write-Host "PlayMode: $($run.GetAttribute('passed')) passed, $($failures.Count) known failures, $($run.GetAttribute('skipped')) skipped."
