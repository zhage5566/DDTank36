param([switch]$FromWatch)
$ErrorActionPreference='Stop'
$taskStartLock=[Threading.Mutex]::new($false,'Local\DDTank36StartLock')
try{$taskOwnLock=$taskStartLock.WaitOne(0)}catch [Threading.AbandonedMutexException]{$taskOwnLock=$true}
if(-not $taskOwnLock){Write-Output 'DDTank36 startup is already in progress';$taskStartLock.Dispose();return}
try{
$taskRoot=$PSScriptRoot
if($FromWatch -and (Test-Path -LiteralPath (Join-Path $taskRoot 'config\maintenance.flag'))){Write-Output 'Maintenance enabled; recovery skipped';return}
$taskPackage=Join-Path $taskRoot 'package\DanDanTang'
$taskLog=Join-Path $taskRoot 'logs'
$taskStamp=Get-Date -Format 'yyyyMMdd-HHmmss'
New-Item -ItemType Directory -Path $taskLog -Force | Out-Null
$taskSqlExe=Join-Path $env:ProgramFiles 'Microsoft SQL Server\160\Tools\Binn\SqlLocalDB.exe'
if(!(Test-Path -LiteralPath $taskSqlExe)){$taskSqlExe=(Get-Command SqlLocalDB.exe -ErrorAction Stop).Source}
$taskSqlStarted=$false;$taskSqlMessage=@()
for($taskSqlAttempt=0;$taskSqlAttempt -lt 4;$taskSqlAttempt++){
 $taskSavedErrorPreference=$ErrorActionPreference
 try{$ErrorActionPreference='Continue';$taskSqlMessage=@(& $taskSqlExe start DDTank36 2>&1);$taskSqlExit=$LASTEXITCODE}finally{$ErrorActionPreference=$taskSavedErrorPreference}
 if($taskSqlExit -eq 0){$taskSqlStarted=$true;break}
 if($taskSqlAttempt -lt 3){Start-Sleep -Seconds 2}
}
if(!$taskSqlStarted){throw ('Could not start LocalDB instance DDTank36: '+($taskSqlMessage -join ' '))}
$taskSpecs=@(
 @{Name='Fight';Path='Server\Fight';Exe='Fight.Local.exe';Port=9408},
 @{Name='WorldFight';Path='Server\World.Fight';Exe='Fight.Local.exe';Port=9409},
 @{Name='Center';Path='Server\Center';Exe='Center.Local.exe';Port=9402},
 @{Name='Road';Path='Server\12313\Road\Road';Exe='Road.Local.exe';Port=9400}
)
foreach($taskSpec in $taskSpecs){
 $taskDir=Join-Path $taskPackage $taskSpec.Path;$taskExe=Join-Path $taskDir $taskSpec.Exe
 $taskExisting=Get-CimInstance Win32_Process | Where-Object ExecutablePath -EQ $taskExe
 if($taskExisting){
  $taskListener=Get-NetTCPConnection -LocalPort $taskSpec.Port -State Listen -ErrorAction SilentlyContinue
  if(-not $taskListener -or $taskListener.OwningProcess -notin $taskExisting.ProcessId){throw "$($taskSpec.Name) running without expected listener; inspect logs"}
  Write-Output "$($taskSpec.Name) already running on $($taskSpec.Port)";continue
 }
 if(Get-NetTCPConnection -LocalPort $taskSpec.Port -State Listen -ErrorAction SilentlyContinue){throw "Port occupied: $($taskSpec.Port)"}
 $taskProc=Start-Process -FilePath $taskExe -ArgumentList '--start' -WorkingDirectory $taskDir -WindowStyle Hidden -RedirectStandardOutput (Join-Path $taskLog "$($taskSpec.Name)-$taskStamp-out.log") -RedirectStandardError (Join-Path $taskLog "$($taskSpec.Name)-$taskStamp-err.log") -PassThru
 Write-Output "Starting $($taskSpec.Name) PID $($taskProc.Id)"
 $taskDeadline=(Get-Date).AddSeconds(20)
 do {
  Start-Sleep -Milliseconds 400
  $taskProc.Refresh()
  if($taskProc.HasExited){throw "$($taskSpec.Name) exited; inspect $taskLog"}
  $taskListener=Get-NetTCPConnection -LocalPort $taskSpec.Port -State Listen -ErrorAction SilentlyContinue
 } until($taskListener -or (Get-Date) -ge $taskDeadline)
 if(-not $taskListener){throw "$($taskSpec.Name) did not open port $($taskSpec.Port); inspect $taskLog"}
 Write-Output "$($taskSpec.Name) listening on $($taskSpec.Port)"
}
$taskIisConfig=Join-Path $taskRoot 'config\applicationhost.config'
if(-not(Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'iisexpress.exe' -and $_.CommandLine.Contains($taskIisConfig) })){
 if(Get-NetTCPConnection -LocalPort 8342 -State Listen -ErrorAction SilentlyContinue){throw 'HTTP port 8342 occupied'}
 $taskIis=Join-Path ${env:ProgramFiles(x86)} 'IIS Express\iisexpress.exe'
 if(!(Test-Path -LiteralPath $taskIis)){$taskIis=Join-Path $env:ProgramFiles 'IIS Express\iisexpress.exe'}
 $taskProc=Start-Process -FilePath $taskIis -ArgumentList ('/config:"'+$taskIisConfig+'"'),'/site:DDTank36' -WorkingDirectory $taskRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $taskLog "IIS-$taskStamp-out.log") -RedirectStandardError (Join-Path $taskLog "IIS-$taskStamp-err.log") -PassThru
 Write-Output "Started IIS PID $($taskProc.Id)"
}
$taskDeadline=(Get-Date).AddSeconds(50);$taskReady=$false
do{
 try{$taskHeaders=@{'X-DDT-Token'=[IO.File]::ReadAllText((Join-Path $taskRoot 'config\runtime-token.txt')).Trim()};$taskState=Invoke-RestMethod 'http://127.0.0.1:9440/status' -Headers $taskHeaders -TimeoutSec 3;$taskReady=$taskState.pool.online -eq 50}catch{}
 if($taskReady){break};Start-Sleep -Seconds 2
}until((Get-Date) -ge $taskDeadline)
if(!$taskReady){throw 'Road listener exists but bot runtime is not ready; inspect the newest Road error log'}
if($taskState.shutdown){[void](Invoke-RestMethod 'http://127.0.0.1:9440/resume' -Method Post -Headers $taskHeaders -TimeoutSec 5)}
Write-Output 'Verified 50 online bots. DDTank 3.6: http://127.0.0.1:8342/Register/UserLogin.aspx'
if(Test-Path -LiteralPath (Join-Path $taskRoot 'Refresh-NativeRankings36.ps1')){& (Join-Path $taskRoot 'Refresh-NativeRankings36.ps1') -Root $taskRoot -Apply | Out-File -LiteralPath (Join-Path $taskRoot 'logs\ranking-refresh.log') -Append}
if(Test-Path -LiteralPath (Join-Path $taskRoot 'config\maintenance.flag')){Remove-Item -LiteralPath (Join-Path $taskRoot 'config\maintenance.flag') -Force}
}finally{$taskStartLock.ReleaseMutex();$taskStartLock.Dispose()}

