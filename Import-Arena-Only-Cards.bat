@echo off
call "%~dp0Import-All-Magic-Cards.bat" --arena-only %*
exit /b %errorlevel%
