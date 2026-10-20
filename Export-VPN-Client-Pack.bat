@echo off
cd /d "%~dp0"
python tools\export_vpn_client_pack.py
if errorlevel 1 pause
