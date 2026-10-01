Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$runnerRoot = Join-Path $PSScriptRoot '.runner'
$listener = Join-Path $runnerRoot 'bin/Runner.Listener.exe'
if (-not (Test-Path -LiteralPath (Join-Path $runnerRoot '.runner'))) {
    throw 'Register the GitHub Actions runner before starting it. See Tools/CI/README.md.'
}
$running = Get-Process -Name Runner.Listener -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq $listener }
if ($running) {
    Write-Output 'ClayWars runner is already running.'
    exit 0
}
$process = Start-Process -FilePath $env:ComSpec -ArgumentList @('/d', '/c', ('"' + (Join-Path $runnerRoot 'run.cmd') + '"')) `
    -WorkingDirectory $runnerRoot -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput (Join-Path $runnerRoot 'host.stdout.log') `
    -RedirectStandardError (Join-Path $runnerRoot 'host.stderr.log')
Write-Output "Started ClayWars runner (launcher PID $($process.Id))."
