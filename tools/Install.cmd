@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-ClientCenter.ps1"
if errorlevel 1 pause
endlocal
