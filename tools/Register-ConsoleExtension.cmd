@echo off
setlocal
set "LOGDIR=%ProgramData%\Client Center for Configuration Manager"
if not exist "%LOGDIR%" mkdir "%LOGDIR%"
set "LOG=%LOGDIR%\console-extension.log"
echo ===== %DATE% %TIME% =====>>"%LOG%"
echo script=%~f0>>"%LOG%"
echo args=%*>>"%LOG%"
echo dir=%~dp0>>"%LOG%"
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0Register-ConsoleExtension.ps1" %* >>"%LOG%" 2>&1
echo powershell_exit=%ERRORLEVEL%>>"%LOG%"
exit /b 0
