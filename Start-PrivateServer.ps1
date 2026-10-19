param([switch]$Restart)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
if ($Restart) {
    Write-Output 'Restarting local services. Any current match will end.'
    $localProcesses = Get-CimInstance Win32_Process | Where-Object {
        $_.CommandLine -and $_.CommandLine.Contains($PSScriptRoot, [System.StringComparison]::OrdinalIgnoreCase) -and (
            ($_.Name -eq 'dotnet.exe' -and $_.CommandLine -match 'ProtocolHost.dll') -or
            ($_.Name -eq 'java.exe' -and $_.CommandLine -match 'ForgeEngineMain'))
    }
    $backendListeners = Get-NetTCPConnection -State Listen -LocalPort 8787 -ErrorAction SilentlyContinue
    foreach ($listener in $backendListeners) {
        $backendProcess = Get-CimInstance Win32_Process -Filter "ProcessId=$($listener.OwningProcess)"
        if ($backendProcess.Name -match '^python' -and $backendProcess.CommandLine -match '-m server') {
            Stop-Process -Id $backendProcess.ProcessId -Force -ErrorAction SilentlyContinue
        }
    }
    $localProcesses | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
}
$stateDirectory = Join-Path $PSScriptRoot 'data\local-client'
& python (Join-Path $PSScriptRoot 'tools\install_custom_arena_cards.py')
if ($LASTEXITCODE -ne 0) { throw 'Could not load the local Arena card pack. Close Arena before installing or updating it.' }
Write-Output 'Checking the card catalogue. A refresh after updates can take a few minutes...'
& python -u (Join-Path $PSScriptRoot 'tools\sync_card_catalogue.py')
if ($LASTEXITCODE -ne 0) { throw 'Could not synchronize the installed card catalogue. See the card sync diagnostic above.' }
$manifestVersionPath = Join-Path $stateDirectory 'manifest-version.json'
if (Test-Path -LiteralPath $manifestVersionPath) {
    $manifestIdentity = Get-Content -LiteralPath $manifestVersionPath -Raw | ConvertFrom-Json
    $connectionIdentity = Get-Content -LiteralPath (Join-Path $stateDirectory 'configuration-state.json') -Raw | ConvertFrom-Json
    $installedVersionPath = Join-Path $connectionIdentity.installation 'version'
    if (Test-Path -LiteralPath $installedVersionPath) {
        $installedIdentity = Get-Content -LiteralPath $installedVersionPath -Raw | ConvertFrom-Json
        if (($manifestIdentity.versions | ConvertTo-Json -Compress) -ne ($installedIdentity.Versions | ConvertTo-Json -Compress)) {
            throw 'Arena updated after the local asset manifests were configured. Refresh the local manifests for the installed client before starting; stale prefabs can leave the match board empty.'
        }
    }
}
$certificatePrefix = Join-Path $stateDirectory 'tls\localhost'
$frontDoorPort = 9445
$matchDoorPort = 9444
foreach ($required in @('bootstrap.json', 'configuration-state.json', 'tls\localhost.pfx', 'tls\localhost.password', 'tls\localhost.cer')) {
    if (!(Test-Path -LiteralPath (Join-Path $stateDirectory $required))) { throw "Local connection setup missing: $required" }
}
if (!(Get-Process MTGA -ErrorAction SilentlyContinue)) {
    & python (Join-Path $PSScriptRoot 'tools\install_mode_labels.py')
    if ($LASTEXITCODE -ne 0) { throw 'Could not load the local game-mode names. See the localization diagnostic above.' }
}
$certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new("$certificatePrefix.cer")
if ($certificate.NotAfter -lt (Get-Date)) { throw 'The localhost certificate expired; restore and rerun connection setup' }
$trustPath = if (Test-Path -LiteralPath "$certificatePrefix.root.cer") { "$certificatePrefix.root.cer" } else { "$certificatePrefix.cer" }
$trustCertificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($trustPath)
if (!(Test-Path -LiteralPath ('Cert:\CurrentUser\Root\' + $trustCertificate.Thumbprint))) { throw 'The local certificate issuer is not trusted for this Windows user' }
$processes = @{}
if (!(Get-NetTCPConnection -State Listen -LocalPort 8787 -ErrorAction SilentlyContinue)) {
    $pythonExecutable = (Get-Command python -ErrorAction Stop).Source
    $process = Start-Process -FilePath $pythonExecutable -ArgumentList @('-m', 'server', '--port', '8787') -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $stateDirectory 'backend.stdout.log') -RedirectStandardError (Join-Path $stateDirectory 'backend.stderr.log') -PassThru
    $processes.backend = $process.Id
}
if (!(Get-NetTCPConnection -State Listen -LocalPort $frontDoorPort -ErrorAction SilentlyContinue)) {
    $dotnetExecutable = (Get-Command dotnet -ErrorAction Stop).Source
    $dllPath = Join-Path $PSScriptRoot 'protocol-host\bin\live\ProtocolHost.dll'
    & $dotnetExecutable build (Join-Path $PSScriptRoot 'protocol-host') -o (Join-Path $PSScriptRoot 'protocol-host\bin\live') | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Protocol host build failed; see dotnet build output before launching.' }
    $process = Start-Process -FilePath $dotnetExecutable -ArgumentList @($dllPath, '--port', "$frontDoorPort", '--match-port', "$matchDoorPort", '--backend-url', 'http://127.0.0.1:8787', '--pfx', "$certificatePrefix.pfx") -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $stateDirectory 'frontdoor.stdout.log') -RedirectStandardError (Join-Path $stateDirectory 'frontdoor.stderr.log') -PassThru
    $processes.frontdoor = $process.Id
}
$processes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stateDirectory 'started-processes.json')
Write-Output 'Waiting for Forge rules and AI to load. The first startup can take a few minutes...'
$engineStatus = $null
for ($attempt = 0; $attempt -lt 10; $attempt++) {
    try {
        $engineStatus = Invoke-RestMethod -Uri 'http://127.0.0.1:8787/api/interactive/status' -TimeoutSec 180
        break
    } catch {
        if ($attempt -eq 9) { throw 'Forge did not become ready. Check data\local-client\backend.stderr.log.' }
        Start-Sleep -Milliseconds 500
    }
}
if (!$engineStatus.ready) { throw 'Forge rules engine is not ready.' }
for ($attempt = 0; $attempt -lt 20; $attempt++) {
    $frontDoorReady = Get-NetTCPConnection -State Listen -LocalPort $frontDoorPort -ErrorAction SilentlyContinue
    $matchDoorReady = Get-NetTCPConnection -State Listen -LocalPort $matchDoorPort -ErrorAction SilentlyContinue
    if ($frontDoorReady -and $matchDoorReady) { break }
    Start-Sleep -Milliseconds 500
}
if (!$frontDoorReady -or !$matchDoorReady) { throw "Protocol host did not bind FrontDoor $frontDoorPort and MatchDoor $matchDoorPort. Check data\local-client\frontdoor.stdout.log and frontdoor.stderr.log." }
Write-Output "Local services ready: backend 127.0.0.1:8787; Arena FrontDoor localhost:$frontDoorPort; MatchDoor localhost:$matchDoorPort; Forge rules and AI loaded."
Write-Output 'Launch MTGA.exe and select Local. Arena match integration is experimental; a complete playable match has not been verified.'
