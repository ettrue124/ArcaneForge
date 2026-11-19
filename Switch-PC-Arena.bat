@echo off
rem Switches PC MTG Arena between this private server and Wizards' official servers.
rem Private = Enable-LocalClient.ps1, Official = Restore-OfficialClient.ps1. Close Arena first.
setlocal
cd /d "%~dp0"
:menu
cls
if exist "data\local-client\configuration-state.json" (set "MODE=Private server") else (set "MODE=Official servers")
echo PC Arena is set to: %MODE%
echo.
echo   1  Private server
echo   2  Official servers
echo   3  Exit
echo.
choice /c 123 /n /m "Choose 1, 2 or 3: "
if errorlevel 3 goto :eof
if errorlevel 2 goto official
:private
if "%MODE%"=="Private server" (echo Already set to Private server.& goto done)
call :login Save Official || goto done
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Enable-LocalClient.ps1" || goto done
call :login Load Private
echo Start the server ^(Launch-Server.bat^) before opening Arena.
goto done
:official
if "%MODE%"=="Official servers" (echo Already set to Official servers.& goto done)
echo If Windows asks to remove a certificate, click Yes.
call :login Save Private || goto done
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Restore-OfficialClient.ps1" || goto done
call :login Load Official
:done
echo.
pause
goto menu

:login
rem Keeps a separate Remember Me login per mode, saved in this Windows user's registry.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\Swap-ArenaLogin.ps1" -Action %1 -Mode %2
exit /b %errorlevel%
