$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
python -m unittest discover -s tests -v
if ($LASTEXITCODE -ne 0) { throw 'Python tests failed' }
dotnet run --project protocol-host -- --self-test
if ($LASTEXITCODE -ne 0) { throw 'Protocol tests failed' }
