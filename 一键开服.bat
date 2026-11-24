@echo off
setlocal
chcp 65001 >nul
cd /d "%~dp0"
set "DDT36_PS=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
if exist "%SystemRoot%\Sysnative\WindowsPowerShell\v1.0\powershell.exe" set "DDT36_PS=%SystemRoot%\Sysnative\WindowsPowerShell\v1.0\powershell.exe"
"%DDT36_PS%" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-OneClick36.ps1"
set "DDT36_EXIT=%ERRORLEVEL%"
if not "%DDT36_EXIT%"=="0" echo 开服失败，请查看上方错误和 logs 目录。
if /i not "%~1"=="--no-pause" pause
exit /b %DDT36_EXIT%
