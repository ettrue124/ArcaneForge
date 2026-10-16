@echo off
cd /d "%~dp0"
python tools\collect_arena_trace.py
pause
