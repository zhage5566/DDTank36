$ErrorActionPreference='Stop'
try{
 & (Join-Path $PSScriptRoot 'Start-DDTank36.ps1')
 & (Join-Path $PSScriptRoot 'Check-Server36.ps1')
 $taskWatch=Get-ScheduledTask -TaskName DDTank36ResidentServer -ErrorAction SilentlyContinue
 if(!$taskWatch -or $taskWatch.State -ne 'Running'){& (Join-Path $PSScriptRoot 'Install-Watch36.ps1') -Root $PSScriptRoot}
 Write-Output 'Login: http://127.0.0.1:8342/Register/UserLogin.aspx'
 Write-Output 'GM: http://127.0.0.1:8342/GM/'
 exit 0
}catch{Write-Error $_ -ErrorAction Continue;exit 1}
