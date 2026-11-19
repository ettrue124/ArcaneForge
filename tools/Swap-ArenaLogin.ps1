# Keeps a separate "Remember Me" login for each PC Arena mode (Official / Private).
# Arena stores one refresh token for both, so switching modes would otherwise force a fresh login.
# Saved logins stay in this Windows user's registry (HKCU), beside Arena's own copy. Values are never printed.
param(
    [Parameter(Mandatory)][ValidateSet('Save', 'Load')][string]$Action,
    [Parameter(Mandatory)][ValidateSet('Official', 'Private')][string]$Mode
)
$ErrorActionPreference = 'Stop'
$arenaKey = 'HKCU:\Software\Wizards Of The Coast\MTGA'
$storeKey = 'HKCU:\Software\ArcaneForge\SavedArenaLogin\' + $Mode
$patterns = '^WAS-RefreshTokenE_h\d+$', '^WAS-RememberMe_h\d+$', '^LastLoginString_h\d+$', '^LOGIN-PreviousEnvironment_h\d+$'

function Copy-LoginValues($from, $to, [switch]$ClearTarget) {
    # Never use New-Item -Force on an existing key: it can recreate the key and drop Arena's other settings.
    if (!(Test-Path -LiteralPath $to)) { New-Item -Path $to -Force | Out-Null }
    $target = Get-Item -LiteralPath $to
    if ($ClearTarget) {
        foreach ($name in $target.Property) {
            if ($patterns | Where-Object { $name -match $_ }) { Remove-ItemProperty -LiteralPath $to -Name $name }
        }
    }
    if (!(Test-Path -LiteralPath $from)) { return 0 }
    $source = Get-Item -LiteralPath $from
    $count = 0
    foreach ($name in $source.Property) {
        if (!($patterns | Where-Object { $name -match $_ })) { continue }
        New-ItemProperty -LiteralPath $to -Name $name -PropertyType $source.GetValueKind($name) -Value $source.GetValue($name) -Force | Out-Null
        $count++
    }
    return $count
}

if (Get-Process MTGA -ErrorAction SilentlyContinue) { throw 'Close MTG Arena first' }
if ($Action -eq 'Save') {
    $n = Copy-LoginValues $arenaKey $storeKey -ClearTarget
    Write-Output "Saved $Mode login ($n settings)."
} else {
    # Clear Arena's current login first so a token for the other mode is never sent to the wrong server.
    $n = Copy-LoginValues $storeKey $arenaKey -ClearTarget
    if ($n) { Write-Output "Restored saved $Mode login." }
    else { Write-Output "No saved $Mode login yet; log in once with Remember Me ticked." }
}
