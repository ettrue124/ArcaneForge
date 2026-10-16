@echo off
cd /d "%~dp0"
python tools\extract_arena_actions.py %*
set "extractionStatus=%errorlevel%"
if not "%extractionStatus%"=="0" (
    echo Action extraction failed or incomplete. See the diagnostics above.
) else (
    echo Open README.md or actions.json in the report folder shown above.
)
pause
exit /b %extractionStatus%
