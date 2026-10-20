@echo off
cd /d "%~dp0"
python "%~dp0tools\vpn_relay.py" --check
if errorlevel 1 goto failed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Restart-Arena.ps1"
if errorlevel 1 goto failed
python "%~dp0tools\vpn_relay.py" --host
:failed
pause
