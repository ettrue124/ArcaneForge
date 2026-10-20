@echo off
setlocal
title Restart ArcaneForge and Arena
cd /d "%~dp0"
echo Restarting Arena will end your current match.
echo Using the fast restart path. Changed fixes install automatically.
echo Leave this window open until the restart finishes.
echo.
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Restart-Arena.ps1" %*
if errorlevel 1 (
    echo.
    echo Restart failed. Read the message above. Arena has not been reopened.
    pause
    exit /b 1
)
echo.
echo Restart complete. Arena is opening and the local services are ready.
exit /b 0
