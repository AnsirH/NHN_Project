Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$file = [IO.Path]::GetTempFileName()
$validator = Join-Path $PSScriptRoot 'Test-PlayModeResults.ps1'
$passed = '<test-run total="1" passed="1" failed="0" skipped="0" inconclusive="0" result="Passed" end-time="2026-10-04 00:00:01Z" duration="1"><test-case fullname="Example.Test" result="Passed" /></test-run>'
# This exact failure was previously allowed; repairs must restore a strict gate.
$baseline = '<test-run total="1" passed="0" failed="1" skipped="0" inconclusive="0" result="Failed" end-time="2026-10-04 00:00:01Z" duration="1"><test-case fullname="OutGame.Tests.PlayMode.ArmyDeploymentPanelPlayTests.ArmyInfoPopup_ShowsCurrentGold" result="Failed"><failure><message>System.NullReferenceException : Object reference not set to an instance of an object</message><stack-trace>ArmyDeploymentPanelPlayTests.cs:911</stack-trace></failure></test-case></test-run>'
function Assert-Rejected([string]$xml, [string]$label) {
    [IO.File]::WriteAllText($file, $xml)
    $rejected = $false
    try { & $validator -ResultsPath $file | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw "Guard accepted $label." }
}
try {
    [IO.File]::WriteAllText($file, $passed)
    & $validator -ResultsPath $file | Out-Null
    Assert-Rejected $baseline 'previously allowed failing test'
    Assert-Rejected ($passed.Replace('result="Passed" end-time', 'result="Cancelled" end-time')) 'cancelled run'
    Assert-Rejected ($passed.Replace('total="1"', 'total="2"')) 'incomplete run'
    Assert-Rejected ($passed.Replace('failed="0"', 'failed="1"')) 'suite failure'
    Assert-Rejected ($baseline.Replace('ArmyInfoPopup_ShowsCurrentGold', 'NewUnexpectedTest')) 'new failing test'
    Assert-Rejected ($passed.Replace('passed="1"', 'passed="0"').Replace('inconclusive="0"', 'inconclusive="1"')) 'inconclusive run'
    Write-Host 'CI guards: passing report accepted; six invalid/failing reports rejected.'
} finally {
    Remove-Item -LiteralPath $file -ErrorAction SilentlyContinue
}
