Add-Type -Path 'client-inspection\Wizards.MDN.GreProtobuf.dll'

Write-Host "--- CastingTimeOptionType ---"
[Enum]::GetValues([Wotc.Mtgo.Gre.External.Messaging.CastingTimeOptionType]) | ForEach-Object { Write-Host "$_ = $([int]$_)" }

Write-Host "--- ManaColor ---"
[Enum]::GetValues([Wotc.Mtgo.Gre.External.Messaging.ManaColor]) | ForEach-Object { Write-Host "$_ = $([int]$_)" }
