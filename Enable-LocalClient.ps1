param([string]$Installation = 'C:\Program Files\Wizards of the Coast\MTGA')
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$installationRoot = (Resolve-Path -LiteralPath $Installation).Path
if (!(Test-Path -LiteralPath (Join-Path $installationRoot 'MTGA.exe'))) { throw 'MTGA.exe is missing' }
if (Get-Process MTGA -ErrorAction SilentlyContinue) { throw 'Close MTG Arena before enabling local services' }
$stateDirectory = Join-Path $projectRoot 'data\local-client'
$statePath = Join-Path $stateDirectory 'configuration-state.json'
if (Test-Path -LiteralPath $statePath) { throw 'Local configuration already saved; restore it before enabling again' }
$certificatePrefix = Join-Path $stateDirectory 'tls\localhost'
if (!(Test-Path -LiteralPath "$certificatePrefix.cer")) { throw 'Generate the local certificate with --create-local-cert first' }
$certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new("$certificatePrefix.cer")
if ($certificate.Subject -ne 'CN=localhost' -or $certificate.NotAfter -lt (Get-Date)) { throw 'Expected a valid localhost certificate' }
$trustPath = if (Test-Path -LiteralPath "$certificatePrefix.root.cer") { "$certificatePrefix.root.cer" } else { "$certificatePrefix.cer" }
$trustCertificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($trustPath)
$configurationPath = [System.IO.Path]::GetFullPath((Join-Path $installationRoot 'MTGA_Data\StreamingAssets\services.conf'))
if (!$configurationPath.StartsWith($installationRoot + '\', [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Configuration target is outside the installation' }
$previousConfiguration = Test-Path -LiteralPath $configurationPath
$backupPath = Join-Path $stateDirectory 'services.conf.backup'
if ($previousConfiguration) { Copy-Item -LiteralPath $configurationPath -Destination $backupPath }
$preferencesPath = 'HKCU:\Software\Wizards Of The Coast\MTGA'
$preferencesBackup = Join-Path $stateDirectory 'preferences-before.reg'
if (Test-Path -LiteralPath $preferencesPath) {
    & reg.exe export 'HKCU\Software\Wizards Of The Coast\MTGA' $preferencesBackup /y | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not preserve Arena preferences' }
}
$alreadyTrusted = Test-Path -LiteralPath ('Cert:\CurrentUser\Root\' + $trustCertificate.Thumbprint)
$configuration = @{
    '$environments' = @('Local')
    environments = @{ Local = @{
        fdHost = 'localhost'; fdPort = 9445; mdHost = 'localhost'; mdPort = 9444
        doorbellUri = 'http://127.0.0.1:8787'
        accountSystemBaseUri = 'http://127.0.0.1:8787/'
        accountSystemId = 'arena-local'; accountSystemSecret = 'local-only'
        accountSystemEnvironment = 1; HostPlatform = 1
        ecoUri = 'http://127.0.0.1:8787/eco'; bikeUri = 'http://127.0.0.1:8787/local-business-events'
    } }
} | ConvertTo-Json -Depth 6
$state = @{
    installation = $installationRoot; configurationPath = $configurationPath
    previousConfiguration = $previousConfiguration; backupPath = $backupPath
    preferencesBackup = $preferencesBackup
    certificateThumbprint = $trustCertificate.Thumbprint; certificateAlreadyTrusted = $alreadyTrusted
    createdAt = (Get-Date).ToUniversalTime().ToString('o')
}
$state | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding utf8
try {
    if (!$alreadyTrusted) { Import-Certificate -FilePath $trustPath -CertStoreLocation 'Cert:\CurrentUser\Root' | Out-Null }
    [System.IO.File]::WriteAllText($configurationPath, $configuration, [System.Text.UTF8Encoding]::new($false))
    $state.configurationSha256 = (Get-FileHash -LiteralPath $configurationPath -Algorithm SHA256).Hash
    $state | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding utf8
} catch {
    Write-Warning 'Setup failed; use Restore-OfficialClient.ps1 to restore the saved state.'
    throw
}
Write-Output 'Arena now selects Local services on its next launch. Only local account credentials will work.'
Write-Output 'Restore official configuration with .\Restore-OfficialClient.ps1.'
