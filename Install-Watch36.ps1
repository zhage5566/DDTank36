param([string]$Root=$PSScriptRoot)
$ErrorActionPreference='Stop'
$taskWinPs=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
$taskWatchArgs='-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File "'+(Join-Path $Root 'Watch-DDTank36.ps1')+'"'
try {
 $taskExisting=Get-ScheduledTask -TaskName DDTank36ResidentServer -ErrorAction SilentlyContinue
 if($taskExisting -and $taskExisting.State -eq 'Running'){Stop-ScheduledTask -TaskName DDTank36ResidentServer;for($taskAttempt=0;$taskAttempt -lt 20;$taskAttempt++){if((Get-ScheduledTask DDTank36ResidentServer).State -ne 'Running'){break};Start-Sleep -Milliseconds 200}}
 $taskIdentity=[Security.Principal.WindowsIdentity]::GetCurrent()
 $taskPrincipal=New-ScheduledTaskPrincipal -UserId $taskIdentity.User.Value -LogonType Interactive -RunLevel Highest
 $taskAction=New-ScheduledTaskAction -Execute $taskWinPs -Argument $taskWatchArgs -WorkingDirectory $Root
 $taskTrigger=New-ScheduledTaskTrigger -AtLogOn -User $taskIdentity.Name
 $taskSettings=New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances IgnoreNew -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
 [void](Register-ScheduledTask -TaskName 'DDTank36ResidentServer' -Action $taskAction -Trigger $taskTrigger -Principal $taskPrincipal -Settings $taskSettings -Force)
 Start-ScheduledTask -TaskName 'DDTank36ResidentServer'
 # Task Scheduler launches the watcher outside the deploying terminal's lifetime.
 Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name DDTank36ResidentServer -ErrorAction SilentlyContinue
 Write-Output 'Resident watcher registered for this Windows user at logon and started.'
} catch {
 Write-Warning ('Task Scheduler unavailable; using current-user startup: '+$_.Exception.Message)
 $taskRunKey='HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
 [void](New-Item $taskRunKey -Force)
 [void](New-ItemProperty $taskRunKey -Name DDTank36ResidentServer -Value ('"'+$taskWinPs+'" '+$taskWatchArgs) -PropertyType String -Force)
 [void](Start-Process $taskWinPs -ArgumentList $taskWatchArgs -WindowStyle Hidden -PassThru)
}
