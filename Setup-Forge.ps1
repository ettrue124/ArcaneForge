param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$forgeDirectory = Join-Path $PSScriptRoot 'vendor\forge'
$dataDirectory = Join-Path $PSScriptRoot 'data'
$bridgeDirectory = Join-Path $PSScriptRoot 'engine-bridge'
$buildDirectory = Join-Path $bridgeDirectory 'build'
New-Item -ItemType Directory -Force -Path $dataDirectory,$buildDirectory | Out-Null
if (!(Test-Path -LiteralPath (Join-Path $forgeDirectory 'pom.xml'))) {
    git clone --depth 1 https://github.com/Card-Forge/forge.git $forgeDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Forge download failed' }
}
# Use Windows certificate trust for Maven's HTTPS downloads, without disabling TLS validation.
$previousJavaOptions = $env:JAVA_TOOL_OPTIONS
try {
    if ($IsWindows -or $env:OS -eq 'Windows_NT') {
        $env:JAVA_TOOL_OPTIONS = ($previousJavaOptions + ' -Djavax.net.ssl.trustStoreType=Windows-ROOT -Djavax.net.ssl.trustStore=NONE').Trim()
    }
    Push-Location -LiteralPath $forgeDirectory
    try {
        if (!$SkipBuild) {
            mvn -B -q -pl forge-gui-desktop -am install '-DskipTests=true' '-Dlaunch4j.skip=true' '-Dassembly.skipAssembly=true' '-Dcheckstyle.skip=true' *> (Join-Path $dataDirectory 'forge-build.log')
            if ($LASTEXITCODE -ne 0) { throw 'Forge build failed. See data/forge-build.log.' }
        }
        $classpathPath = Join-Path $buildDirectory 'forge-classpath.txt'
        mvn -B -q -pl forge-gui-desktop -am dependency:build-classpath "-Dmdep.outputFile=$classpathPath" *> (Join-Path $dataDirectory 'forge-classpath.log')
        if ($LASTEXITCODE -ne 0) { throw 'Forge dependency resolution failed. See data/forge-classpath.log.' }
    } finally { Pop-Location }
    $targetClasses = @(
        Join-Path $forgeDirectory 'forge-gui-desktop\target\classes',
        Join-Path $forgeDirectory 'forge-gui\target\classes',
        Join-Path $forgeDirectory 'forge-game\target\classes',
        Join-Path $forgeDirectory 'forge-ai\target\classes',
        Join-Path $forgeDirectory 'forge-core\target\classes'
    ) -join [IO.Path]::PathSeparator
    $classpath = $targetClasses + [IO.Path]::PathSeparator + (Get-Content -LiteralPath $classpathPath -Raw).Trim()
    & python (Join-Path $PSScriptRoot 'tools\export_linked_faces.py')
    if ($LASTEXITCODE -ne 0) { throw 'Linked card face export failed' }
    & python (Join-Path $PSScriptRoot 'tools\export_generated_cards.py')
    if ($LASTEXITCODE -ne 0) { throw 'Generated card export failed' }
    $bridgeSources = @(Get-ChildItem -LiteralPath $bridgeDirectory -Filter '*.java' | ForEach-Object FullName)
    $bridgeSources += @(Get-ChildItem -LiteralPath (Join-Path $bridgeDirectory 'overrides') -Filter '*.java' -Recurse | ForEach-Object FullName)
    javac -encoding UTF-8 -cp $classpath -d $buildDirectory $bridgeSources
    if ($LASTEXITCODE -ne 0) { throw 'Forge bridge compilation failed' }
    $profileDirectory = Join-Path $dataDirectory 'forge-profile'
    $appData = Join-Path $profileDirectory 'appdata'
    $localAppData = Join-Path $profileDirectory 'localappdata'
    New-Item -ItemType Directory -Force -Path $profileDirectory,$appData,$localAppData | Out-Null
    $javaExecutable = (Get-Command java).Source
    $command = @($javaExecutable, '-Xmx2048m', '-Djava.awt.headless=true', '-Dfile.encoding=UTF-8', "-Duser.home=$profileDirectory", '-cp', ($buildDirectory + [IO.Path]::PathSeparator + $classpath), 'ForgeEngineMain', (Join-Path $forgeDirectory 'forge-gui'))
    $revision = (git -C $forgeDirectory rev-parse HEAD).Trim()
    @{command=$command;cwd=$PSScriptRoot;defaultTimeout=180;env=@{APPDATA=$appData;LOCALAPPDATA=$localAppData};forgeCommit=$revision} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $bridgeDirectory 'launch.json') -Encoding utf8NoBOM
    Write-Output "Forge bridge ready at commit $revision"
} finally { $env:JAVA_TOOL_OPTIONS = $previousJavaOptions }
