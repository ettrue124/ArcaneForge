@echo off
rem Turns off the iPhone Arena redirect: removes /Documents/services.conf so Arena uses Wizards' official servers.
rem Phone must be plugged in by USB and unlocked. Fully close and reopen Arena afterwards.
"%~dp0data\ios\device-tools-env\Scripts\python.exe" "%~dp0tools\iphone_arena_switch.py" official
echo.
echo If it says "Official servers", fully close Arena on the phone and reopen it.
pause
