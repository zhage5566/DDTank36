param([string]$Root=$PSScriptRoot,[string]$BaseUrl='http://127.0.0.1:8342',[switch]$Apply)
$ErrorActionPreference='Stop'
function Get-RankHash36([string]$Path){$algorithm=[Security.Cryptography.SHA256]::Create();$stream=[IO.File]::OpenRead($Path);try{[BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$algorithm.Dispose()}}
$taskUri=[Uri]$BaseUrl
if(!$taskUri.IsLoopback -or $taskUri.Scheme -ne 'http' -or $taskUri.Port -ne 8342){throw 'Only the local 3.6 HTTP service on port 8342 is allowed.'}
$taskRequest=Join-Path $Root 'package\DanDanTang\Site\Request'
# All names come from the 3.6 TofflistRightView; these are local, non-cross rankings.
$taskNames=@('CelebByDayFightPowerList','CelebByDayGPList','CelebByWeekGPList','CelebByGPList','CelebByAchievementPointDayList','CelebByAchievementPointWeekList','CelebByAchievementPointList','CelebByDayGiftGp','CelebByWeekGiftGp','CelebByGiftGpList','CelebByWeekLeagueScore','CelebByConsortiaFightPower','CelebByConsortiaLevel','CelebByConsortiaDayRiches','CelebByConsortiaWeekRiches','CelebByConsortiaRiches','CelebByConsortiaDayGiftGp','CelebByConsortiaWeekGiftGp','CelebByConsortiaGiftGp')
if(!$Apply){$taskNames | ForEach-Object {$BaseUrl.TrimEnd('/')+'/Request/CelebList/'+$_+'.ashx'};Write-Output 'Preview only. Pass -Apply to call the original generators; no rank values are written by this script.';return}
$taskRankLock=[Threading.Mutex]::new($false,'Local\DDTank36RankRefresh')
try{$taskOwnRankLock=$taskRankLock.WaitOne(30000)}catch [Threading.AbandonedMutexException]{$taskOwnRankLock=$true}
if(!$taskOwnRankLock){$taskRankLock.Dispose();Write-Output 'A native rank refresh is already running.';return}
try{
$taskBackup=Join-Path $Root ('config\ranking-backup\'+(Get-Date -Format 'yyyyMMdd'))
[void][IO.Directory]::CreateDirectory($taskBackup)
$taskResults=@()
foreach($taskName in $taskNames){
 $taskHandler=Join-Path (Join-Path $taskRequest 'CelebList') ($taskName+'.ashx')
 if(!(Test-Path -LiteralPath $taskHandler)){throw "Missing native rank handler: $taskName"}
 $taskPlain=Join-Path $taskRequest ($taskName+'_Out.xml');$taskPacked=Join-Path $taskRequest ($taskName+'.xml')
 $taskPrevious=@{};foreach($taskFile in @($taskPlain,$taskPacked)){if(Test-Path -LiteralPath $taskFile){$taskPrevious[$taskFile]=[IO.File]::ReadAllBytes($taskFile)}}
 foreach($taskFile in @($taskPlain,$taskPacked)){if((Test-Path -LiteralPath $taskFile) -and !(Test-Path -LiteralPath (Join-Path $taskBackup ([IO.Path]::GetFileName($taskFile))))){Copy-Item -LiteralPath $taskFile -Destination (Join-Path $taskBackup ([IO.Path]::GetFileName($taskFile))) -Force}}
 try{
  $taskCall=[Net.HttpWebRequest]::Create($BaseUrl.TrimEnd('/')+'/Request/CelebList/'+$taskName+'.ashx');$taskCall.Proxy=$null;$taskCall.AllowAutoRedirect=$false;$taskCall.Timeout=60000
  $taskResponse=$taskCall.GetResponse();try{$taskReader=[IO.StreamReader]::new($taskResponse.GetResponseStream());try{$taskBody=$taskReader.ReadToEnd()}finally{$taskReader.Dispose()}}finally{$taskResponse.Dispose()}
  if($taskBody -notmatch ('Build:'+ [regex]::Escape($taskName) +'\.xml,Success!')){throw ('Native generator did not report successful cache write: '+$taskBody)}
  if(!(Test-Path -LiteralPath $taskPacked)){throw 'The native generator did not write its cache.'}
  # An _Out companion is optional for some original handlers; compressed data is
  # authoritative to the Flash client. Validate it using the zlib header/raw stream.
  $taskBytes=[IO.File]::ReadAllBytes($taskPacked)
  if($taskBytes.Length -lt 8 -or $taskBytes[0] -ne 120){throw 'Expected original zlib-compressed XML cache.'}
  $taskStream=[IO.MemoryStream]::new($taskBytes,2,$taskBytes.Length-6,$false)
  $taskInflate=[IO.Compression.DeflateStream]::new($taskStream,[IO.Compression.CompressionMode]::Decompress)
  $taskText=[IO.StreamReader]::new($taskInflate,[Text.Encoding]::UTF8)
  try{$taskXml=[Xml.XmlDocument]::new();$taskXml.LoadXml($taskText.ReadToEnd())}finally{$taskText.Dispose();$taskInflate.Dispose();$taskStream.Dispose()}
  if($taskXml.DocumentElement.GetAttribute('value').ToLowerInvariant() -ne 'true'){throw 'Native ranking reported failure; restoring cache.'}
  $taskResults+=[pscustomobject]@{Rank=$taskName;Entries=$taskXml.SelectNodes('/Result/Item').Count;SHA256=(Get-RankHash36 $taskPacked)}
 }catch{
  foreach($taskFile in @($taskPlain,$taskPacked)){if($taskPrevious.ContainsKey($taskFile)){[IO.File]::WriteAllBytes($taskFile,$taskPrevious[$taskFile])}elseif(Test-Path -LiteralPath $taskFile){Remove-Item -LiteralPath $taskFile -Force}}
  throw
 }
}
[pscustomobject]@{Time=(Get-Date).ToString('o');NativeGenerators=$taskResults;Backup=$taskBackup;RankValuesModified=$false} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $Root 'config\ranking-last-refresh.json') -Encoding UTF8
$taskResults | Format-Table -AutoSize

}finally{$taskRankLock.ReleaseMutex();$taskRankLock.Dispose()}
