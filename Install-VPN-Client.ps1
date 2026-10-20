param([string]$Installation='C:\Program Files\Wizards of the Coast\MTGA')
$ErrorActionPreference='Stop'
Set-Location -LiteralPath $PSScriptRoot
if (Get-Process MTGA -ErrorAction SilentlyContinue) { throw 'Close Arena before installing the host display pack' }
$vpnPack=Get-Content -LiteralPath data/vpn-client-pack.json -Raw | ConvertFrom-Json
$vpnInstalledVersion=(Get-Content -LiteralPath (Join-Path $Installation 'version') -Raw | ConvertFrom-Json).Versions.PSObject.Properties.Name
if ($vpnInstalledVersion.Count -ne 1 -or $vpnInstalledVersion -ne $vpnPack.version) { throw 'The Arena version must match the host before installing this pack' }
$vpnWorkspace=[System.IO.Path]::GetFullPath($PSScriptRoot)+[System.IO.Path]::DirectorySeparatorChar
foreach ($entry in $vpnPack.files) {
    $vpnFile=[System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot $entry.path))
    if (!$vpnFile.StartsWith($vpnWorkspace,[System.StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid pack path' }
    if ((Get-FileHash -LiteralPath $vpnFile -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) { throw "Damaged display pack file: $($entry.path)" }
}
if (!(Test-Path -LiteralPath scripts/refresh_client.py)) { throw 'Extract this pack into a matching ArcaneForge source checkout first' }
if (Test-Path -LiteralPath data/local-client/configuration-state.json) { throw 'Restore your existing local-client configuration before switching hosts, then run this installer again' }
& python scripts/refresh_client.py "$Installation"
if ($LASTEXITCODE -ne 0) { throw 'Client import failed' }
& "$PSScriptRoot\Enable-LocalClient.ps1" -Installation $Installation
Copy-Item -LiteralPath data/vpn-server-catalogue.json -Destination data/catalogue.json
& python tools/install_custom_arena_cards.py
if ($LASTEXITCODE -ne 0) { throw 'Display pack installation failed; existing installation backups were preserved' }
& python tools/install_mode_labels.py
if ($LASTEXITCODE -ne 0) { throw 'Mode label installation failed' }
Write-Host 'Host display pack installed. Run Join-VPN-Server.bat, then open Arena and sign in to your own server account.'
