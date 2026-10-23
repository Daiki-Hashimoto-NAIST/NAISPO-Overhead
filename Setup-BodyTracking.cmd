@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Setup-BodyTracking.ps1" -Download %*
set "setupResult=%ERRORLEVEL%"
if not "%setupResult%"=="0" pause
exit /b %setupResult%
