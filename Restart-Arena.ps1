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
    & (Join-Path $PSScriptRoot 'Start-PrivateServer.ps1') -Restart -FullRefresh:$FullRefresh
    Start-Process -FilePath $restartExecutable -WorkingDirectory $restartInstallation -WindowStyle Hidden
    Write-Host ('Arena reopened. Restart took {0:N1} seconds.' -f $restartTimer.Elapsed.TotalSeconds)
} catch {
    Write-Host ('Restart failed: ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
exit 0
