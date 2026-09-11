@echo off
chcp 65001 >nul
setlocal
set "MAIN=%~dp0..\Object1688.Main.exe"
if not exist "%MAIN%" set "MAIN=%~dp0..\dist\publish-win-x64\Main\Object1688.Main.exe"
if not exist "%MAIN%" set "MAIN=%~dp0..\src\Object1688.Main\bin\Debug\net8.0-windows\Object1688.Main.exe"
if not exist "%MAIN%" (
  echo [ERROR] Object1688.Main.exe not found. Build or publish first.
  pause >nul
  exit /b 1
)
echo Stopping Object1688 ...
"%MAIN%" --control quit
if errorlevel 1 (
  echo.
  echo [NOTE] If it says the control interface is unreachable, the app is not running.
) else (
  echo.
  echo [OK] Quit command sent. Object1688 should exit within a couple of seconds.
)
echo.
echo [done] press any key to close...
pause >nul
