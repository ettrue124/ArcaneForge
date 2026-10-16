@echo off
setlocal
cd /d "%~dp0"
title Arena repair and gameplay checks
echo Rebuilding known adapters and checking gameplay. This can take several minutes.
echo Missing visual behavior will be listed in the report; full animation parity is not assumed.
echo.
python -u tools\repair_arena_animations.py
set "result=%errorlevel%"
echo.
if not "%result%"=="0" echo Some checks need attention. See the report path above.
pause
exit /b %result%
