param([string]$Root=$PSScriptRoot)
$ErrorActionPreference='Stop'
$taskServices=@(
 @{Port=9408;Relative='package\DanDanTang\Server\Fight\Fight.Local.exe'},
 @{Port=9409;Relative='package\DanDanTang\Server\World.Fight\Fight.Local.exe'},
 @{Port=9402;Relative='package\DanDanTang\Server\Center\Center.Local.exe'},
 @{Port=9400;Relative='package\DanDanTang\Server\12313\Road\Road\Road.Local.exe'}
)
$taskProcesses=Get-CimInstance Win32_Process
foreach($taskService in $taskServices){
 $taskExpected=[IO.Path]::GetFullPath((Join-Path $Root $taskService.Relative))
 $taskOwner=@($taskProcesses | Where-Object ExecutablePath -EQ $taskExpected)
 $taskListen=@(Get-NetTCPConnection -LocalPort $taskService.Port -State Listen -ErrorAction SilentlyContinue)
 if($taskOwner.Count -ne 1 -or !$taskListen -or @($taskListen | Where-Object OwningProcess -NotIn $taskOwner.ProcessId).Count){throw "Service/port ownership check failed: $($taskService.Port)"}
}
$taskHeaders=@{'X-DDT-Token'=[IO.File]::ReadAllText((Join-Path $Root 'config\runtime-token.txt')).Trim()}
$taskState=Invoke-RestMethod 'http://127.0.0.1:9440/status' -Headers $taskHeaders -TimeoutSec 10
if($taskState.shutdown -or $taskState.pool.online -ne 50){throw 'Bot runtime is not ready with 50 online bots'}
$taskBots=@($taskState.bots | Where-Object online)
if($taskBots.Count -ne 50 -or @($taskBots | Where-Object {!$_.registered -or $_.power -le 0}).Count){throw 'Online bots must be registered with native positive combat power'}
$taskPage=Invoke-WebRequest 'http://127.0.0.1:8342/Register/UserLogin.aspx' -UseBasicParsing -TimeoutSec 15
if($taskPage.StatusCode -ne 200){throw 'Login page health check failed'}
Write-Output 'PASS: Road/Fight/WorldFight/Center ports belong to this version; login HTTP 200; 50 registered online bots.'
