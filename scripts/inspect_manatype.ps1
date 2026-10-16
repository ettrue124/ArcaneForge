Add-Type -Path 'client-inspection\Wizards.MDN.GreProtobuf.dll'
Add-Type -Path 'client-inspection\Wizards.Arena.Models.dll'
Add-Type -Path 'client-inspection\SharedClientCore.dll'

$t = [System.Type]::GetType("Wotc.Mtga.DuelScene.Interactions.CastingTimeOption_ManaTypeWorkflow, SharedClientCore")
if ($t) {
    Write-Host "Found workflow: $($t.FullName)"
    $t.GetMethods() | ForEach-Object { Write-Host "$($_.ReturnType.Name) $($_.Name)" }
} else {
    Write-Host "Searching all types for ManaTypeWorkflow..."
    [AppDomain]::CurrentDomain.GetAssemblies() | ForEach-Object {
        try {
            $_.GetTypes() | Where-Object { $_.Name -like "*ManaType*" } | ForEach-Object { Write-Host $_.FullName }
        } catch {}
    }
}
