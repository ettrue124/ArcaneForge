@echo off
cd /d "%~dp0"
python tools\map_arena_animations.py
if errorlevel 1 echo Animation inventory failed. See the error above.
pause
