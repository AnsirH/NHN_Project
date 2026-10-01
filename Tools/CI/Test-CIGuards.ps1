Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$file = [IO.Path]::GetTempFileName()
$validator = Join-Path $PSScriptRoot 'Test-PlayModeResults.ps1'
$allSignatures = Get-Content (Join-Path $PSScriptRoot 'KnownPlayModeSignatures.json') -Raw | ConvertFrom-Json
$known = $allSignatures[0]
$name = [Security.SecurityElement]::Escape($known.name)
$message = [Security.SecurityElement]::Escape($known.messageContains[0])
$stack = [Security.SecurityElement]::Escape($known.stackContains)
$baseline = '<test-run total="1" passed="0" failed="1" skipped="0" inconclusive="0" result="Failed" end-time="2026-10-01 00:00:01Z" duration="1"><test-case fullname="' + $name + '" result="Failed"><failure><message>' + $message + '</message><stack-trace>' + $stack + '</stack-trace></failure></test-case></test-run>'
function Assert-Rejected([string]$xml, [string]$label) {
    [IO.File]::WriteAllText($file, $xml)
    $rejected = $false
    try { & $validator -ResultsPath $file | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw "Guard accepted $label." }
}
try {
    [IO.File]::WriteAllText($file, $baseline)
    & $validator -ResultsPath $file | Out-Null
    Assert-Rejected ($baseline.Replace('result="Failed" end-time', 'result="Cancelled" end-time')) 'cancelled run'
    Assert-Rejected ($baseline.Replace('total="1"', 'total="2"')) 'incomplete run'
    Assert-Rejected ($baseline.Replace('failed="1"', 'failed="2"')) 'suite failure'
    Assert-Rejected ($baseline.Replace($name, 'New.Unexpected.Test')) 'new failing test'
    Assert-Rejected ($baseline.Replace($message, 'System.InvalidOperationException')) 'changed failure reason'
    Assert-Rejected ($baseline.Replace($stack, 'OtherTests.cs:1')) 'changed failure location'
    Write-Host 'CI guards: baseline accepted; six invalid/failing reports rejected.'
} finally {
    Remove-Item -LiteralPath $file -ErrorAction SilentlyContinue
}
