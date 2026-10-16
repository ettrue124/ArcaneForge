param([int]$Port = 8787, [string]$ClientSource = 'MTGA.zip')
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
if (!(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'data\catalogue.json'))) {
    python scripts/prepare_client.py $ClientSource
    if ($LASTEXITCODE -ne 0) { throw 'Client preparation failed' }
}
python -m server --port $Port
