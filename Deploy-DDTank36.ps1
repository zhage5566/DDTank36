param([string]$Archive='', [switch]$CheckOnly, [switch]$NoStart)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath($PSScriptRoot)
$taskPackage=Join-Path $taskRoot 'package\DanDanTang'
$taskConfig=Join-Path $taskRoot 'config'
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskProgramFiles64=$env:ProgramW6432
if(!$taskProgramFiles64){$taskProgramFiles64=$env:ProgramFiles}
function Find-SqlLocalDb36 {
 $taskExpected=Join-Path $taskProgramFiles64 'Microsoft SQL Server\160\Tools\Binn\SqlLocalDB.exe'
 if(Test-Path -LiteralPath $taskExpected){return $taskExpected}
 $taskFound=Get-Command SqlLocalDB.exe -ErrorAction SilentlyContinue
 if($taskFound -and (Test-Path -LiteralPath $taskFound.Source)){
  if([Diagnostics.FileVersionInfo]::GetVersionInfo($taskFound.Source).FileMajorPart -ge 16){return $taskFound.Source}
 }
 return $taskExpected
}
$taskSqlExe=Find-SqlLocalDb36
$taskIis=Join-Path ${env:ProgramFiles(x86)} 'IIS Express\iisexpress.exe'
if(!(Test-Path -LiteralPath $taskIis)){$taskIis=Join-Path $env:ProgramFiles 'IIS Express\iisexpress.exe'}
if(!$Archive){
 foreach($taskCandidate in @((Join-Path $taskRoot 'satoishgift.zip'),(Join-Path $taskRoot 'payload\satoishgift.zip'),(Join-Path $env:USERPROFILE 'Downloads\DDTank-3.6-Frank-CN\satoishgift.zip'),'D:\Downloads\DDTank-3.6-Frank-CN\satoishgift.zip')){
  if(Test-Path -LiteralPath $taskCandidate){$Archive=$taskCandidate;break}
 }
}
function Check-Prerequisites36 {
 if(![Environment]::Is64BitOperatingSystem){throw 'This package requires 64-bit Windows and SQL Server 2022 LocalDB x64.'}
 if(![Environment]::Is64BitProcess){throw 'Run the one-click BAT so that 64-bit Windows PowerShell is used.'}
 $taskRelease=(Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -ErrorAction SilentlyContinue).Release
 if(!$taskRelease -or $taskRelease -lt 461808 -or !(Test-Path -LiteralPath $taskCompiler)){throw 'Install Microsoft .NET Framework 4.7.2 or 4.8 first, then run again.'}
 foreach($taskName in @('Configure-Package36.ps1','applicationhost.template.config','Build-Extensions.ps1','Start-DDTank36.ps1','Stop-DDTank36.ps1','Watch-DDTank36.ps1','Install-Watch36.ps1','Refresh-NativeRankings36.ps1','Repair-PetTemplates36.ps1','config\sources\Bots36.cs','config\sources\GM\Api.cs','config\lib\0Harmony.dll','config\Branding36.Apply.ps1','config\Branding36.DatabaseApply.ps1')){
  if(!(Test-Path -LiteralPath (Join-Path $taskRoot $taskName))){throw "Missing deployment file: $taskName"}
 }
 if(!(Test-Path -LiteralPath $taskPackage) -and (!$Archive -or !(Test-Path -LiteralPath $Archive))){throw 'Original satoishgift.zip is required. Put it beside this script or pass -Archive <absolute path>.'}
}
function Install-Prerequisite36([string]$Name,[string]$Path){
 if(!(Test-Path -LiteralPath $Path)){throw "Missing prerequisite $Name installer: $Path"}
 $taskSignature=Get-AuthenticodeSignature -LiteralPath $Path
 if($taskSignature.Status -ne 'Valid' -or $taskSignature.SignerCertificate.Subject -notmatch 'Microsoft Corporation'){throw "Microsoft signature verification failed: $Name"}
 $taskLogDirectory=Join-Path $taskRoot 'logs';[void][IO.Directory]::CreateDirectory($taskLogDirectory)
 $taskLogName='install-'+($Name -replace '[^A-Za-z0-9]+','-')+'-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.log'
 $taskInstallerLog=Join-Path $taskLogDirectory $taskLogName
 $taskArguments='/i "'+$Path+'" /qn /norestart /L*v "'+$taskInstallerLog+'"'
 # Required by this Microsoft MSI's InstallExecuteSequence when UILevel != 5.
 # Without this property, first-time silent LocalDB installation is rejected.
 if([IO.Path]::GetFileName($Path) -ieq 'SqlLocalDB.msi'){$taskArguments+=' IACCEPTSQLLOCALDBLICENSETERMS=YES'}
 Write-Output "Installing $Name ..."
 Write-Output "Installer log: $taskInstallerLog"
 $taskInstall=Start-Process -FilePath 'msiexec.exe' -ArgumentList $taskArguments -WindowStyle Hidden -Wait -PassThru
 if($taskInstall.ExitCode -eq 3010 -or $taskInstall.ExitCode -eq 1641){throw "$Name installed, but Windows requires a restart. Restart Windows and run the BAT again. Installer log: $taskInstallerLog"}
 if($taskInstall.ExitCode -ne 0){
  if(Test-Path -LiteralPath $taskInstallerLog){
   $taskDetails=@(Select-String -LiteralPath $taskInstallerLog -Pattern 'Return value 3|LocalDB_IAcceptSQLLocalDBLicenseTerms|CA_ErrorPendingReboot|Error 1[0-9]{3}|Error 2[0-9]{3}' -Context 3,1 | Select-Object -Last 4)
   if($taskDetails.Count){Write-Output ($taskDetails | Out-String)}
  }
  throw "$Name installation failed: $($taskInstall.ExitCode). Detailed installer log: $taskInstallerLog"
 }
}
function Restore-MissingDatabases36 {
 $taskData=Join-Path $taskRoot 'data';[void][IO.Directory]::CreateDirectory($taskData)
 $taskDb=[Data.SqlClient.SqlConnection]::new('Server=(localdb)\DDTank36;Database=master;Integrated Security=true');$taskDb.Open()
 try{
  foreach($taskSpec in @(@('tank','Db_Tank_S1'),@('newmem','Db_Membership'),@('count','Db_Count'),@('cross','Db_Cross'),@('log','Db_Logs'))){
   $taskName=$taskSpec[1];$taskCmd=$taskDb.CreateCommand();$taskCmd.CommandTimeout=600
   $taskCmd.CommandText='SELECT COUNT(*) FROM sys.databases WHERE name=@name';[void]$taskCmd.Parameters.AddWithValue('@name',$taskName)
   if([int]$taskCmd.ExecuteScalar() -gt 0){Write-Output "Preserving existing database: $taskName";continue}
   $taskCmd.Parameters.Clear();$taskBackup=Join-Path $taskPackage ($taskSpec[0]+'.bak')
   if(!(Test-Path -LiteralPath $taskBackup)){throw "Missing original database backup: $taskBackup"}
   $taskSafeBackup=$taskBackup.Replace("'","''")
   $taskCmd.CommandText="RESTORE VERIFYONLY FROM DISK=N'$taskSafeBackup'";[void]$taskCmd.ExecuteNonQuery()
   $taskCmd.CommandText="RESTORE FILELISTONLY FROM DISK=N'$taskSafeBackup'"
   $taskFiles=[Data.DataTable]::new();$taskAdapter=[Data.SqlClient.SqlDataAdapter]::new($taskCmd);[void]$taskAdapter.Fill($taskFiles)
   $taskMoves=@();$taskIndex=0
   foreach($taskFile in $taskFiles.Rows){
    $taskIndex++;if($taskFile.Type -notin @('D','L')){throw "Unsupported backup file type: $($taskFile.Type)"}
    $taskExt=if($taskFile.Type -eq 'L'){'.ldf'}elseif($taskIndex -eq 1){'.mdf'}else{'.ndf'}
    $taskDest=Join-Path $taskData ($taskName+'-'+$taskIndex+$taskExt)
    if(Test-Path -LiteralPath $taskDest){throw "Orphan database file exists; inspect it before retrying: $taskDest"}
    $taskMoves+="MOVE N'$($taskFile.LogicalName.Replace("'","''"))' TO N'$($taskDest.Replace("'","''"))'"
   }
   $taskCmd.CommandText="RESTORE DATABASE [$taskName] FROM DISK=N'$taskSafeBackup' WITH "+($taskMoves -join ',')
   [void]$taskCmd.ExecuteNonQuery();Write-Output "Restored original database: $taskName"
  }
  $taskCmd=$taskDb.CreateCommand();$taskCmd.CommandText="UPDATE Db_Tank_S1.dbo.Server_List SET IP='127.0.0.1',Port=9400,State=2 WHERE ID=1;IF @@ROWCOUNT<>1 THROW 50000,'Expected original Server_List ID 1',1;";[void]$taskCmd.ExecuteNonQuery()
 }finally{$taskDb.Dispose()}
}
function Expand-Original36 {
 if(Test-Path -LiteralPath $taskPackage){
  foreach($taskRel in @('Server\12313\Road\Road\Road.Service.exe','Server\Fight\Fighting.Service.exe','Site\Flash\config.xml','Site\Resource\flash','tank.bak','newmem.bak','count.bak','cross.bak','log.bak')){
   if(!(Test-Path -LiteralPath (Join-Path $taskPackage $taskRel))){throw "Incomplete package, inspect it before retrying: $taskRel"}
  };Write-Output 'Using existing original package';return
 }
 Add-Type -AssemblyName System.IO.Compression,System.IO.Compression.FileSystem
 $taskStage=Join-Path $taskRoot ('package.incoming-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
 [void][IO.Directory]::CreateDirectory($taskStage)
 # This original ZIP uses GBK for entries without the UTF-8 flag. The default
 # decoder produces replacement characters and can collapse distinct filenames.
 $taskZipEncoding=[Text.Encoding]::GetEncoding(936)
 $taskZip=[IO.Compression.ZipFile]::Open([IO.Path]::GetFullPath($Archive),[IO.Compression.ZipArchiveMode]::Read,$taskZipEncoding)
 try{
  foreach($taskEntry in $taskZip.Entries){
   $taskTarget=[IO.Path]::GetFullPath((Join-Path $taskStage $taskEntry.FullName))
   if(!$taskTarget.StartsWith($taskStage+'\',[StringComparison]::OrdinalIgnoreCase)){throw "Unsafe ZIP entry: $($taskEntry.FullName)"}
  }
 }finally{$taskZip.Dispose()}
 Write-Output 'Extracting original package. This can take several minutes ...'
 [IO.Compression.ZipFile]::ExtractToDirectory([IO.Path]::GetFullPath($Archive),$taskStage,$taskZipEncoding)
 $taskExtracted=Join-Path $taskStage 'DanDanTang'
 if(!(Test-Path -LiteralPath (Join-Path $taskExtracted 'Server\12313\Road\Road\Road.Service.exe'))){throw 'Unexpected original ZIP layout'}
 [void][IO.Directory]::CreateDirectory((Split-Path $taskPackage -Parent))
 Move-Item -LiteralPath $taskExtracted -Destination $taskPackage
}
function New-LocalCredentials36 {
 $taskToken=Join-Path $taskConfig 'runtime-token.txt'
 if(!(Test-Path -LiteralPath $taskToken)){[IO.File]::WriteAllText($taskToken,([Guid]::NewGuid().ToString('N')+[Guid]::NewGuid().ToString('N')),[Text.UTF8Encoding]::new($false))}
 $taskGm=Join-Path $taskPackage 'Site\GM';[void][IO.Directory]::CreateDirectory((Join-Path $taskGm 'bin'))
 $taskWeb=Join-Path $taskGm 'Web.config'
 if(!(Test-Path -LiteralPath $taskWeb)){
  $taskPassword='DDT36-'+[Guid]::NewGuid().ToString('N').Substring(0,20)
  $taskSha=[Security.Cryptography.SHA256]::Create();try{$taskHash=[BitConverter]::ToString($taskSha.ComputeHash([Text.Encoding]::UTF8.GetBytes($taskPassword))).Replace('-','')}finally{$taskSha.Dispose()}
  $taskXml='<configuration><appSettings><add key="AdminHash" value="'+$taskHash+'"/></appSettings><system.web><compilation targetFramework="4.7.2"/><sessionState mode="InProc" timeout="60"/><authentication mode="None"/><customErrors mode="Off"/><globalization requestEncoding="utf-8" responseEncoding="utf-8"/></system.web><system.webServer><defaultDocument><files><clear/><add value="index.html"/></files></defaultDocument></system.webServer></configuration>'
  [IO.File]::WriteAllText($taskWeb,$taskXml,[Text.UTF8Encoding]::new($false))
  [IO.File]::WriteAllText((Join-Path $taskRoot 'GM管理凭据.txt'),("GM: http://127.0.0.1:8342/GM/`r`nPassword: "+$taskPassword+"`r`nLocal access only. Keep this file private.`r`n"),[Text.UTF8Encoding]::new($true))
 }else{Write-Output 'Preserving existing GM password and runtime token'}
}
Check-Prerequisites36
if($CheckOnly){
 [pscustomobject]@{Root=$taskRoot;PackagePresent=(Test-Path -LiteralPath $taskPackage);ArchiveAvailable=[bool]$Archive;LocalDbInstalled=(Test-Path -LiteralPath $taskSqlExe);IisInstalled=(Test-Path -LiteralPath $taskIis);FrameworkCompiler=$taskCompiler;ExistingDataPolicy='Restore missing databases only; preserve accounts and keys'} | Format-List
 return
}
if(!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Run the BAT as administrator. LocalDB and server must run under the same Windows account.'}
$taskDeployLock=[Threading.Mutex]::new($false,'Local\DDTank36DeployLock')
try{$taskOwnLock=$taskDeployLock.WaitOne(0)}catch [Threading.AbandonedMutexException]{$taskOwnLock=$true}
if(!$taskOwnLock){$taskDeployLock.Dispose();throw 'Another 3.6 deployment is running'}
try{
 [void][IO.Directory]::CreateDirectory($taskConfig);[void][IO.Directory]::CreateDirectory((Join-Path $taskRoot 'logs'))
 if(!(Test-Path -LiteralPath $taskSqlExe)){Install-Prerequisite36 'SQL Server 2022 LocalDB' (Join-Path $taskRoot 'prerequisites\SqlLocalDB.msi');$taskSqlExe=Find-SqlLocalDb36}
 if(!(Test-Path -LiteralPath $taskIis)){Install-Prerequisite36 'IIS Express 10 x64' (Join-Path $taskRoot 'prerequisites\iisexpress_amd64_en-US.msi');$taskIis=Join-Path ${env:ProgramFiles(x86)} 'IIS Express\iisexpress.exe'}
 if(!(Test-Path -LiteralPath $taskSqlExe) -or !(Test-Path -LiteralPath $taskIis)){throw 'Prerequisite executable missing after install; reboot if required and retry'}
 $env:PATH=(Split-Path $taskSqlExe -Parent)+';'+$env:PATH
 Expand-Original36
 & (Join-Path $taskRoot 'Stop-DDTank36.ps1')
 $taskInstances=@(& $taskSqlExe info)
 if($LASTEXITCODE -ne 0){throw 'Cannot enumerate LocalDB instances'}
 if($taskInstances -notcontains 'DDTank36'){& $taskSqlExe create DDTank36;if($LASTEXITCODE -ne 0){throw 'Cannot create DDTank36 LocalDB instance'}}
 & $taskSqlExe start DDTank36;if($LASTEXITCODE -ne 0){throw 'Cannot start DDTank36 LocalDB instance'}
 Restore-MissingDatabases36
 & (Join-Path $taskRoot 'Repair-ShopPrices36.ps1')
 & (Join-Path $taskRoot 'Repair-ModelKeys36.ps1') -Root $taskRoot
 & (Join-Path $taskRoot 'Repair-PetTemplates36.ps1') -Root $taskRoot
 & (Join-Path $taskRoot 'Configure-Package36.ps1') -Root $taskRoot
 New-LocalCredentials36
 $taskRoad=Join-Path $taskPackage 'Server\12313\Road\Road'
 foreach($taskDir in @($taskRoad,(Join-Path $taskPackage 'Server\Fight'),(Join-Path $taskPackage 'Server\World.Fight'))){Copy-Item -LiteralPath (Join-Path $taskConfig 'lib\0Harmony.dll') -Destination (Join-Path $taskDir '0Harmony.dll') -Force}
 & (Join-Path $taskRoot 'Build-Extensions.ps1')
 $taskBuild=Join-Path $taskConfig 'build'
 & (Join-Path $taskBuild 'RosterSetup36.exe') $taskRoot;if($LASTEXITCODE -ne 0){throw 'Bot roster setup failed'}
 foreach($taskName in @('Road.Local.exe','Local36.Bots.dll')){Copy-Item -LiteralPath (Join-Path $taskBuild $taskName) -Destination (Join-Path $taskRoad $taskName) -Force}
 foreach($taskDir in @('Server\Fight','Server\World.Fight')){Copy-Item -LiteralPath (Join-Path $taskBuild 'Fight.Local.exe') -Destination (Join-Path $taskPackage ($taskDir+'\Fight.Local.exe')) -Force}
 Copy-Item -LiteralPath (Join-Path $taskBuild 'Center.Local.exe') -Destination (Join-Path $taskPackage 'Server\Center\Center.Local.exe') -Force
 $taskGm=Join-Path $taskPackage 'Site\GM'
 Copy-Item -LiteralPath (Join-Path $taskBuild 'Local36.GM.dll') -Destination (Join-Path $taskGm 'bin\Local36.GM.dll') -Force
 foreach($taskName in @('index.html','api.ashx')){Copy-Item -LiteralPath (Join-Path $taskConfig ('sources\GM\'+$taskName)) -Destination (Join-Path $taskGm $taskName) -Force}
 & (Join-Path $taskConfig 'Branding36.Apply.ps1') -Root $taskRoot
 & (Join-Path $taskConfig 'Branding36.DatabaseApply.ps1') -Root $taskRoot
 if(!$NoStart){
  & (Join-Path $taskRoot 'Start-DDTank36.ps1')
  & (Join-Path $taskRoot 'Install-Watch36.ps1') -Root $taskRoot
  $taskDeadline=(Get-Date).AddSeconds(50);$taskStatus=$null
  do{
   try{$taskHeaders=@{'X-DDT-Token'=[IO.File]::ReadAllText((Join-Path $taskConfig 'runtime-token.txt')).Trim()};$taskStatus=Invoke-RestMethod -Uri 'http://127.0.0.1:9440/status' -Headers $taskHeaders -TimeoutSec 3}catch{}
   if($taskStatus -and $taskStatus.pool.online -eq 50){break}
   Start-Sleep -Seconds 2
  }until((Get-Date) -ge $taskDeadline)
  if(!$taskStatus -or $taskStatus.pool.online -ne 50){throw 'Server started but online pool has not reached 50; inspect logs. Existing data has been preserved.'}
  Write-Output ('Verified online pool: '+$taskStatus.pool.online+'/'+$taskStatus.pool.total)
  Write-Output 'Game: http://127.0.0.1:8342/Register/UserLogin.aspx'
  Write-Output 'GM: http://127.0.0.1:8342/GM/'
 }
 Write-Output ('GM credential file: '+(Join-Path $taskRoot 'GM管理凭据.txt'))
 Write-Output 'Deployment complete. Other DDTank versions are not managed by this script.'
}finally{$taskDeployLock.ReleaseMutex();$taskDeployLock.Dispose()}
