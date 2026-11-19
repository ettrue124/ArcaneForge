param([switch]$RestorePreferences)
$ErrorActionPreference = 'Stop'
if (Get-Process MTGA -ErrorAction SilentlyContinue) { throw 'Close MTG Arena before restoring services' }
& python (Join-Path $PSScriptRoot 'tools\install_mode_labels.py') --restore
if ($LASTEXITCODE -ne 0) { throw 'Could not restore local game-mode labels' }
$statePath = Join-Path $PSScriptRoot 'data\local-client\configuration-state.json'
if (!(Test-Path -LiteralPath $statePath)) { throw 'No saved local configuration state exists' }
$state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
$installationRoot = (Resolve-Path -LiteralPath $state.installation).Path
$configurationPath = [System.IO.Path]::GetFullPath($state.configurationPath)
$expectedPath = [System.IO.Path]::GetFullPath((Join-Path $installationRoot 'MTGA_Data\StreamingAssets\services.conf'))
if ($configurationPath -ne $expectedPath) { throw 'Saved configuration path is unexpected' }
# The project folder may have moved since setup; look beside the state file for saved backups.
$stateDirectory = Split-Path -LiteralPath $statePath
foreach ($name in 'backupPath', 'preferencesBackup') {
    if ($state.$name -and !(Test-Path -LiteralPath $state.$name)) {
        $moved = Join-Path $stateDirectory (Split-Path -Leaf $state.$name)
        if (Test-Path -LiteralPath $moved) { $state.$name = $moved }
    }
}
if ($state.configurationSha256 -and (Test-Path -LiteralPath $configurationPath)) {
    $currentHash = (Get-FileHash -LiteralPath $configurationPath -Algorithm SHA256).Hash
    # A previous partial run may already have put the original back; that is fine to continue from.
    $alreadyRestored = $state.previousConfiguration -and (Test-Path -LiteralPath $state.backupPath) -and
        $currentHash -eq (Get-FileHash -LiteralPath $state.backupPath -Algorithm SHA256).Hash
    if ($currentHash -ne $state.configurationSha256 -and !$alreadyRestored) {
        throw 'services.conf changed after setup; preserve those changes before restoring'
    }
}
if ($state.previousConfiguration) {
    if (!(Test-Path -LiteralPath $state.backupPath)) { throw 'Original services.conf backup not found' }
    Copy-Item -LiteralPath $state.backupPath -Destination $configurationPath -Force
}
elseif (Test-Path -LiteralPath $configurationPath) { Remove-Item -LiteralPath $configurationPath }
if (!$state.certificateAlreadyTrusted) {
    if ($state.certificateThumbprint -notmatch '^[0-9A-F]{40}$') { throw 'Invalid saved certificate identity' }
    $certificatePath = 'Cert:\CurrentUser\Root\' + $state.certificateThumbprint
    if (Test-Path -LiteralPath $certificatePath) { Remove-Item -LiteralPath $certificatePath }
}
if ($RestorePreferences -and (Test-Path -LiteralPath $state.preferencesBackup)) {
    & reg.exe import $state.preferencesBackup | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not restore saved Arena preferences' }
}
Move-Item -LiteralPath $statePath -Destination ($statePath + '.restored-' + (Get-Date -Format 'yyyyMMddHHmmss'))
Write-Output 'Official client service configuration restored. Local server account data is preserved.'
