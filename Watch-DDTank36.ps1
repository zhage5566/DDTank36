$ErrorActionPreference='Continue'
$taskRoot=$PSScriptRoot
$taskLock=[Threading.Mutex]::new($false,'Local\DDTank36ServerWatch')
try{$taskOwnLock=$taskLock.WaitOne(0)}catch [Threading.AbandonedMutexException]{$taskOwnLock=$true}
if(-not $taskOwnLock){$taskLock.Dispose();exit}
$taskSpecs=@(
 @{Name='Fight.Local.exe';Dir='Server\Fight';Port=9408},
 @{Name='Fight.Local.exe';Dir='Server\World.Fight';Port=9409},
 @{Name='Center.Local.exe';Dir='Server\Center';Port=9402},
 @{Name='Road.Local.exe';Dir='Server\12313\Road\Road';Port=9400}
)
$taskNextRankCheck=[DateTime]::MinValue
try{
 while($true){
  if(-not(Test-Path -LiteralPath (Join-Path $taskRoot 'config\maintenance.flag'))){
   $taskRecover=$false
   foreach($taskSpec in $taskSpecs){
    $taskExe=Join-Path $taskRoot ('package\DanDanTang\'+$taskSpec.Dir+'\'+$taskSpec.Name)
    $taskListener=Get-NetTCPConnection -LocalPort $taskSpec.Port -State Listen -ErrorAction SilentlyContinue
    $taskProcs=Get-CimInstance Win32_Process | Where-Object ExecutablePath -EQ $taskExe
    if(-not $taskListener){
     foreach($taskProc in $taskProcs){$taskP=Get-Process -Id $taskProc.ProcessId -ErrorAction SilentlyContinue;if($taskP -and ((Get-Date)-$taskP.StartTime).TotalSeconds -gt 90){Stop-Process -Id $taskProc.ProcessId -Force}}
     if(-not $taskProcs){$taskRecover=$true}
    }
   }
   try{
    $taskHeaders=@{'X-DDT-Token'=[IO.File]::ReadAllText((Join-Path $taskRoot 'config\runtime-token.txt')).Trim()}
    [void](Invoke-RestMethod 'http://127.0.0.1:9440/status' -Headers $taskHeaders -TimeoutSec 3)
   }catch{
    $taskRoadExe=Join-Path $taskRoot 'package\DanDanTang\Server\12313\Road\Road\Road.Local.exe'
    foreach($taskProc in (Get-CimInstance Win32_Process | Where-Object ExecutablePath -EQ $taskRoadExe)){
     $taskP=Get-Process -Id $taskProc.ProcessId -ErrorAction SilentlyContinue
     if($taskP -and ((Get-Date)-$taskP.StartTime).TotalSeconds -gt 90){Stop-Process -Id $taskProc.ProcessId -Force;$taskRecover=$true}
    }
   }
   $taskIis=Get-CimInstance Win32_Process | Where-Object {$_.Name -eq 'iisexpress.exe' -and $_.CommandLine -like ('*'+$taskRoot+'\config\applicationhost.config*')}
   if(-not $taskIis){$taskRecover=$true}
   if($taskRecover){try{& (Join-Path $taskRoot 'Start-DDTank36.ps1') -FromWatch *>> (Join-Path $taskRoot 'logs\watch-recovery.log')}catch{$_ | Out-File -LiteralPath (Join-Path $taskRoot 'logs\watch-recovery.log') -Append}}
   if([DateTime]::UtcNow -ge $taskNextRankCheck){
    $taskNextRankCheck=[DateTime]::UtcNow.AddMinutes(1)
    $taskRankScript=Join-Path $taskRoot 'Refresh-NativeRankings36.ps1';$taskRankState=Join-Path $taskRoot 'config\ranking-last-refresh.json';$taskRosterFile=Join-Path $taskRoot 'config\bots36.json'
    $taskRankDue=!(Test-Path -LiteralPath $taskRankState)
    if(!$taskRankDue){$taskRankTime=(Get-Item -LiteralPath $taskRankState).LastWriteTimeUtc;$taskRankDue=([DateTime]::UtcNow-$taskRankTime).TotalMinutes -ge 15 -or ((Test-Path -LiteralPath $taskRosterFile) -and (Get-Item -LiteralPath $taskRosterFile).LastWriteTimeUtc -gt $taskRankTime)}
    if($taskRankDue -and (Test-Path -LiteralPath $taskRankScript)){try{& $taskRankScript -Root $taskRoot -Apply *>> (Join-Path $taskRoot 'logs\ranking-refresh.log')}catch{$_ | Out-File -LiteralPath (Join-Path $taskRoot 'logs\ranking-refresh.log') -Append}}
   }
  }
  Start-Sleep -Seconds 10
 }
}finally{$taskLock.ReleaseMutex();$taskLock.Dispose()}

