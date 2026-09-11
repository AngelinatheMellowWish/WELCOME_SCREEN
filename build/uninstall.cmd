@echo off
chcp 65001 >nul
setlocal
echo ============================================================
echo  Object1688 uninstall helper
echo  Removes: HKCU autostart entry + %%APPDATA%%\Object1688
echo  (pass -AlsoCleanLogs to also delete the log\ folder)
echo ============================================================
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0uninstall.ps1" %*
echo.
echo [done] press any key to close...
pause >nul
