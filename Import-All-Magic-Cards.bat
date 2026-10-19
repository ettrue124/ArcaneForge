@echo off
setlocal
title Import Magic cards
cd /d "%~dp0"

where python >nul 2>&1
if errorlevel 1 (
    echo Python was not found. Install Python or use the Python setup for this project.
    pause
    exit /b 1
)

echo Close Arena before installing cards. Your local server will not be restarted.
echo Downloaded card data and artwork are reused when you run this again.
echo Leave this window open until the import finishes.
echo.
python -u "%~dp0tools\import_magic_cards.py" --install %*
set "magicImportExit=%errorlevel%"
echo.
if "%magicImportExit%"=="0" (
    echo Import finished. See the report for added and skipped cards.
    echo Restart your local server when ready, then reopen Arena.
) else (
    echo Import did not finish successfully. Read the message above before trying again.
)
echo Report: %~dp0data\magic-import\report.txt
echo Guide: %~dp0ALL-MAGIC-CARDS.md
pause
exit /b %magicImportExit%
