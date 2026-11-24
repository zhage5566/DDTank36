@echo off
chcp 65001 >nul
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; $t=Get-ScheduledTask -TaskName DDTank36ResidentServer -ErrorAction SilentlyContinue; if($t){Disable-ScheduledTask -TaskName DDTank36ResidentServer | Out-Null}; Remove-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name DDTank36ResidentServer -ErrorAction SilentlyContinue; Write-Host '已取消本项目登录自启动；未删除数据。需要立即关服请运行一键停服。'"
pause
