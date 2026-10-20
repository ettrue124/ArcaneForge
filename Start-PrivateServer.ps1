param([switch]$Restart, [switch]$FullRefresh)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
function Test-LocalListener([int]$Port) {
    # Avoid repeatedly starting the slow Windows networking provider.
    foreach ($endpoint in [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()) {
        if ($endpoint.Port -eq $Port) { return $true }
    }
    return $false
}
if ($Restart) {
    Write-Output 'Restarting local services. Any current match will end.'
    $restartProcesses = Get-CimInstance Win32_Process
    $localProcesses = $restartProcesses | Where-Object {
        $_.CommandLine -and $_.CommandLine.IndexOf($PSScriptRoot, [System.StringComparison]::OrdinalIgnoreCase) -ge 0 -and (
            ($_.Name -eq 'dotnet.exe' -and $_.CommandLine -match 'ProtocolHost.dll') -or
            ($_.Name -eq 'java.exe' -and $_.CommandLine -match 'ForgeEngineMain'))
    }
    $backendProcess = $null
    $startedPath = Join-Path $PSScriptRoot 'data\local-client\started-processes.json'
    if (Test-Path -LiteralPath $startedPath) {
        try {
            $started = Get-Content -LiteralPath $startedPath -Raw | ConvertFrom-Json
            $backendProcess = $restartProcesses | Where-Object { $_.ProcessId -eq $started.backend } | Select-Object -First 1
            if (!$backendProcess -or !$started.backendStartedAtUtc -or $backendProcess.Name -notmatch '^python' -or
                $backendProcess.CommandLine -notmatch '-m server' -or
                [math]::Abs(($backendProcess.CreationDate.ToUniversalTime() - ([datetime]::Parse($started.backendStartedAtUtc)).ToUniversalTime()).TotalMilliseconds) -gt 10) {
                $backendProcess = $null
            }
        } catch { $backendProcess = $null }
    }
    if (!$backendProcess -and (Test-LocalListener 8787)) {
        $backendListener = Get-NetTCPConnection -State Listen -LocalPort 8787 -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($backendListener) {
            $backendProcess = $restartProcesses | Where-Object { $_.ProcessId -eq $backendListener.OwningProcess } | Select-Object -First 1
        }
    }
    if ($backendProcess) {
        if ($backendProcess.Name -match '^python' -and $backendProcess.CommandLine -match '-m server') {
            Stop-Process -Id $backendProcess.ProcessId -Force -ErrorAction SilentlyContinue
        }
    }
    $localProcesses | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
}
$stateDirectory = Join-Path $PSScriptRoot 'data\local-client'
$preparationArguments = @('-u', (Join-Path $PSScriptRoot 'tools\prepare_startup.py'))
if ($FullRefresh) { $preparationArguments += '--full-refresh' }
& python @preparationArguments
if ($LASTEXITCODE -ne 0) { throw 'Could not prepare local card data. See the diagnostic above; close Arena if an update needs installation.' }
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
if (!(Test-LocalListener 8787)) {
    $pythonExecutable = (Get-Command python -ErrorAction Stop).Source
    $process = Start-Process -FilePath $pythonExecutable -ArgumentList @('-m', 'server', '--port', '8787', '--warm-engine') -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $stateDirectory 'backend.stdout.log') -RedirectStandardError (Join-Path $stateDirectory 'backend.stderr.log') -PassThru
    $processes.backend = $process.Id
    $processes.backendStartedAtUtc = $process.StartTime.ToUniversalTime().ToString('o')
}
if (!(Test-LocalListener $frontDoorPort)) {
    $dotnetExecutable = (Get-Command dotnet -ErrorAction Stop).Source
    $dllPath = Join-Path $PSScriptRoot 'protocol-host\bin\live\ProtocolHost.dll'
    & python (Join-Path $PSScriptRoot 'tools\prepare_protocol_host.py')
    if ($LASTEXITCODE -ne 0) { throw 'Protocol host build failed; see dotnet build output before launching.' }
    $process = Start-Process -FilePath $dotnetExecutable -ArgumentList @($dllPath, '--port', "$frontDoorPort", '--match-port', "$matchDoorPort", '--backend-url', 'http://127.0.0.1:8787', '--pfx', "$certificatePrefix.pfx") -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $stateDirectory 'frontdoor.stdout.log') -RedirectStandardError (Join-Path $stateDirectory 'frontdoor.stderr.log') -PassThru
    $processes.frontdoor = $process.Id
}
if ($processes.Count -gt 0) {
    $processes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stateDirectory 'started-processes.json')
}
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
    $frontDoorReady = Test-LocalListener $frontDoorPort
    $matchDoorReady = Test-LocalListener $matchDoorPort
    if ($frontDoorReady -and $matchDoorReady) { break }
    Start-Sleep -Milliseconds 500
}
if (!$frontDoorReady -or !$matchDoorReady) { throw "Protocol host did not bind FrontDoor $frontDoorPort and MatchDoor $matchDoorPort. Check data\local-client\frontdoor.stdout.log and frontdoor.stderr.log." }
Write-Output "Local services ready: backend 127.0.0.1:8787; Arena FrontDoor localhost:$frontDoorPort; MatchDoor localhost:$matchDoorPort; Forge rules and AI loaded."
Write-Output 'Launch MTGA.exe and select Local. Arena match integration is experimental; a complete playable match has not been verified.'
