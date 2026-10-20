param([string]$ServerAddress)
$ErrorActionPreference='Stop'
Set-Location -LiteralPath $PSScriptRoot
if (!$ServerAddress) { $ServerAddress=Read-Host 'Host Tailscale address (100.x.x.x)' }
$vpnServerAddress=$null
if (![System.Net.IPAddress]::TryParse($ServerAddress,[ref]$vpnServerAddress)) { throw 'Enter a valid VPN IP address' }
Write-Host 'Close your local server first. Keep this connection window open while playing.'
& python tools/vpn_relay.py --connect "$vpnServerAddress"
if ($LASTEXITCODE -ne 0) { throw 'VPN connection could not start' }
