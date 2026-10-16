import os, subprocess

cmd = r'''
$files = Get-ChildItem -Path "client-inspection\*.dll"
Add-Type -Path "client-inspection\Wizards.MDN.GreProtobuf.dll"
Add-Type -Path "client-inspection\Wizards.Arena.Models.dll"
Add-Type -Path "client-inspection\Wizards.Arena.Models.Protobuf.dll"
Add-Type -Path "client-inspection\SharedClientCore.dll"

[AppDomain]::CurrentDomain.GetAssemblies() | ForEach-Object {
    $_.GetTypes() | Where-Object { $_.Name -like "*Modal*" -or $_.Name -like "*CastingTimeOption*" } | ForEach-Object {
        $_.FullName
    }
}
'''
subprocess.run(["powershell", "-Command", cmd])
