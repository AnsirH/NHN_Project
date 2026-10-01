Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../Build/WebGL')).Path
foreach ($name in @('index.html', 'build-info.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $name))) { throw "Missing WebGL file: $name" }
}
$files = @(Get-ChildItem -LiteralPath (Join-Path $root 'Build') -File)
foreach ($pattern in @('*.loader.js', '*.wasm.unityweb', '*.data.unityweb', '*.framework.js.unityweb')) {
    $matches = @($files | Where-Object Name -Like $pattern)
    if ($matches.Count -ne 1) { throw "Expected exactly one $pattern file; found $($matches.Count)." }
    if ($matches[0].Length -eq 0) { throw "Empty build file: $($matches[0].Name)" }
}
$info = Get-Content -Raw -LiteralPath (Join-Path $root 'build-info.json') | ConvertFrom-Json
if ($env:GITHUB_SHA -and $info.revision -ne $env:GITHUB_SHA) { throw 'Build revision does not match the workflow commit.' }
$size = (Get-ChildItem -LiteralPath $root -Recurse -File | Measure-Object Length -Sum).Sum
if ($size -ge 1GB) { throw 'WebGL output exceeds the GitHub Pages 1 GB site limit.' }
Write-Output "WebGL artifact validated: $([math]::Round($size / 1MB, 1)) MB, revision $($info.revision)."
