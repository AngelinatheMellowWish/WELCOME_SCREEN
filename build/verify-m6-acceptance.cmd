@echo off
chcp 65001 >nul
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0verify-m6-acceptance.ps1" %*
echo.
echo [done] press any key to close...
pause >nul
