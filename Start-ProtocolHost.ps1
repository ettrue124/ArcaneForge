param([int]$Port = 9443, [switch]$Match, [switch]$LocalAuth, [string]$Pfx)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$hostArguments = @('run', '--project', 'protocol-host', '--', '--port', "$Port")
if ($Match) { $hostArguments += '--match' }
if ($LocalAuth) { $hostArguments += @('--backend-url', 'http://127.0.0.1:8787') }
if (!$Pfx -and $LocalAuth) {
    $localPfx = Join-Path $PSScriptRoot 'data\local-client\tls\localhost.pfx'
    if (Test-Path -LiteralPath $localPfx) { $Pfx = $localPfx }
}
$previousPassword = $env:ARENA_PFX_PASSWORD
try {
    if ($Pfx) {
        $hostArguments += @('--pfx', $Pfx)
        $passwordFile = [System.IO.Path]::ChangeExtension($Pfx, '.password')
        if (Test-Path -LiteralPath $passwordFile) { $env:ARENA_PFX_PASSWORD = (Get-Content -LiteralPath $passwordFile -Raw).Trim() }
    }
    & dotnet @hostArguments
    if ($LASTEXITCODE -ne 0) { throw 'Protocol host failed' }
} finally { $env:ARENA_PFX_PASSWORD = $previousPassword }
