@echo off
setlocal
title Arena Private Server
cd /d "%~dp0"
echo Starting Arena private server...
echo.
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-PrivateServer.ps1"
if errorlevel 1 (
    echo.
    echo Server startup failed. See the error above.
    pause
    exit /b 1
)
echo.
echo Startup script finished. Server services run in the background.
echo Logs: %~dp0data\local-client
pause
exit /b 0
