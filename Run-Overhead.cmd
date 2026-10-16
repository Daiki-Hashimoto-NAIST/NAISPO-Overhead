@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Launch-Overhead.ps1"
if errorlevel 1 pause
