@echo off
setlocal

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Build-Plugin.ps1"
set "BUILD_EXIT_CODE=%ERRORLEVEL%"

endlocal & exit /b %BUILD_EXIT_CODE%
