@echo off
setlocal
chcp 65001 >nul
cd /d "%~dp0"
set "DDT36_POWERSHELL=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
if exist "%SystemRoot%\Sysnative\WindowsPowerShell\v1.0\powershell.exe" set "DDT36_POWERSHELL=%SystemRoot%\Sysnative\WindowsPowerShell\v1.0\powershell.exe"
"%DDT36_POWERSHELL%" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Deploy-DDTank36.ps1" %*
set "DDT36_RESULT=%ERRORLEVEL%"
if not "%DDT36_RESULT%"=="0" echo 部署失败。请查看上方日志路径；如果提示重启，请重启 Windows 后再运行。
pause
exit /b %DDT36_RESULT%
