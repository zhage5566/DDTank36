$ErrorActionPreference='Stop'
$taskStopLock=[Threading.Mutex]::new($false,'Local\DDTank36StartLock')
try{$taskOwnLock=$taskStopLock.WaitOne(30000)}catch [Threading.AbandonedMutexException]{$taskOwnLock=$true}
if(-not $taskOwnLock){$taskStopLock.Dispose();throw 'Startup still in progress; retry stop after startup finishes'}
try{
$taskRoot=$PSScriptRoot
Set-Content -LiteralPath (Join-Path $taskRoot 'config\maintenance.flag') -Value 'Stopped by version-specific stop script' -Encoding utf8
try{
 $taskHeaders=@{'X-DDT-Token'=[IO.File]::ReadAllText((Join-Path $taskRoot 'config\runtime-token.txt')).Trim()};$taskNoticeDeadline=[DateTime]::UtcNow.AddSeconds(20)
 do{$taskDeparture=Invoke-RestMethod 'http://127.0.0.1:9440/prepare-stop' -Method Post -Headers $taskHeaders -TimeoutSec 3;if($taskDeparture.ready){break};Start-Sleep -Milliseconds 500}while([DateTime]::UtcNow -lt $taskNoticeDeadline)
}catch{Write-Output 'Offline notice endpoint unavailable; continuing the version-specific stop.'}
$taskPackage=Join-Path $taskRoot 'package\DanDanTang'
$taskConfig=Join-Path $taskRoot 'config\applicationhost.config'
foreach($taskProcess in Get-CimInstance Win32_Process) {
 $taskService=$taskProcess.ExecutablePath -and $taskProcess.ExecutablePath.StartsWith($taskPackage+'\',[System.StringComparison]::OrdinalIgnoreCase) -and $taskProcess.Name -in @('Road.Local.exe','Road.Service.exe','Center.Service.exe','Center.Local.exe','Fight.Local.exe','Fighting.Service.exe')
 $taskIis=$taskProcess.Name -eq 'iisexpress.exe' -and $taskProcess.CommandLine.Contains($taskConfig)
 if($taskService -or $taskIis){$taskRunning=Get-Process -Id $taskProcess.ProcessId;Stop-Process -Id $taskProcess.ProcessId;[void]$taskRunning.WaitForExit(10000);Write-Output "Stopped $($taskProcess.Name) PID $($taskProcess.ProcessId)"}
}
}finally{$taskStopLock.ReleaseMutex();$taskStopLock.Dispose()}

