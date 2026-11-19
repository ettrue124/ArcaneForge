param([switch]$FullRefresh)
$ErrorActionPreference = 'Stop'
$restartTimer = [System.Diagnostics.Stopwatch]::StartNew()
try {
    Set-Location -LiteralPath $PSScriptRoot
    $restartConfigPath = Join-Path $PSScriptRoot 'data\local-client\configuration-state.json'
    $restartConfig = Get-Content -LiteralPath $restartConfigPath -Raw | ConvertFrom-Json
    $restartInstallation = [System.IO.Path]::GetFullPath($restartConfig.installation)
    $restartExecutable = Join-Path $restartInstallation 'MTGA.exe'
    if (!(Test-Path -LiteralPath $restartExecutable -PathType Leaf)) {
        throw "Arena was not found at $restartExecutable. Check the local installation configuration."
    }
    Get-Process MTGA -ErrorAction SilentlyContinue | Stop-Process -Force
    # Stop-Process returns before exit; the card installer refuses while MTGA exists.
    # Wait-Process can return early (it errors silently on an exiting process), so poll instead.
    $exitDeadline = (Get-Date).AddSeconds(120)
    while ((Get-Process MTGA -ErrorAction SilentlyContinue) -and (Get-Date) -lt $exitDeadline) { Start-Sleep -Milliseconds 500 }
    if (Get-Process MTGA -ErrorAction SilentlyContinue) { throw 'Arena is still running; close it and restart again.' }
    & (Join-Path $PSScriptRoot 'Start-PrivateServer.ps1') -Restart -FullRefresh:$FullRefresh
    Start-Process -FilePath $restartExecutable -WorkingDirectory $restartInstallation -WindowStyle Normal
    Write-Host ('Arena reopened. Restart took {0:N1} seconds.' -f $restartTimer.Elapsed.TotalSeconds)
} catch {
    Write-Host ('Restart failed: ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
exit 0
