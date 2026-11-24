param([string]$Root='D:\DDTank-3.6-Frank-CN',[string]$Contact='961077680')
$ErrorActionPreference='Stop'
if($Contact -notmatch '^\d{5,12}$'){throw 'Contact must be a numeric QQ identifier.'}
$taskBase=[IO.Path]::GetFullPath($Root)
$taskStamp=Get-Date -Format 'yyyyMMdd-HHmmss'
$taskBackup=Join-Path $taskBase ('config\branding-backup\'+$taskStamp)
$taskChanges=@()
# Only explicit customer-service config fields are rewritten. QQHall protocol keys,
# SMTP domains, usernames, item identifiers and unrelated digit sequences are preserved.
foreach($taskRelative in @('package\DanDanTang\Site\Flash\config.xml','package\DanDanTang\Site\Resource\flash\config.xml')){
 $taskPath=Join-Path $taskBase $taskRelative
 if(!(Test-Path -LiteralPath $taskPath)){throw "Missing client configuration: $taskRelative"}
 $taskBytes=[IO.File]::ReadAllBytes($taskPath)
 $taskUtf8=[Text.UTF8Encoding]::new($false,$true)
 $taskText=$taskUtf8.GetString($taskBytes)
 $taskXml=[xml]$taskText
 $taskNode=$taskXml.SelectSingleNode('//QQ_CS')
 if($null -eq $taskNode){throw "QQ_CS not found: $taskRelative"}
 $taskOld=$taskNode.GetAttribute('value')
 $taskReplacement=[regex]::Replace($taskText,'(<QQ_CS\b[^>]*\bvalue\s*=\s*["''])([^"'']*)(["''])',('${1}'+$Contact+'${3}'))
 if($taskReplacement -eq $taskText){continue}
 [void][xml]$taskReplacement
 $taskCopy=Join-Path $taskBackup $taskRelative
 [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskCopy))
 [IO.File]::WriteAllBytes($taskCopy,$taskBytes)
 [IO.File]::WriteAllBytes($taskPath,$taskUtf8.GetBytes($taskReplacement))
 $taskChanges+=[pscustomobject]@{Path=$taskRelative;Field='QQ_CS';Before=$taskOld;After=$Contact;SHA256=(Get-FileHash -LiteralPath $taskPath -Algorithm SHA256).Hash}
}
# Client-visible language values, never the lookup keys. These are generic share
# captions rather than account credentials or QQ Hall protocol class names.
$taskRelative='package\DanDanTang\Site\Resource\flash\ui\zh_cn\language.txt'
$taskPath=Join-Path $taskBase $taskRelative
$taskBytes=[IO.File]::ReadAllBytes($taskPath)
$taskUtf8=[Text.UTF8Encoding]::new($false,$true);$taskText=$taskUtf8.GetString($taskBytes)
$taskReplacement=$taskText
$taskCaptionMap=@{
 'crazytank.copyOK'=('地址复制成功，游戏联系：qq'+$Contact)
 'Crazytank.share'=('联系qq'+$Contact)
 'feedback.view.csTelNumber'=('游戏联系：qq'+$Contact)
}
foreach($taskKey in $taskCaptionMap.Keys){
 $taskPattern='(?m)^'+[regex]::Escape($taskKey)+':[^\r\n]*'
 $taskReplacement=[regex]::Replace($taskReplacement,$taskPattern,($taskKey+':'+$taskCaptionMap[$taskKey]))
}
if($taskReplacement -ne $taskText){
 $taskCopy=Join-Path $taskBackup $taskRelative
 [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskCopy))
 [IO.File]::WriteAllBytes($taskCopy,$taskBytes)
 [IO.File]::WriteAllBytes($taskPath,$taskUtf8.GetBytes($taskReplacement))
 $taskChanges+=[pscustomobject]@{Path=$taskRelative;Field='client captions';Before='share and support display values';After=('qq'+$Contact);SHA256=(Get-FileHash -LiteralPath $taskPath -Algorithm SHA256).Hash}
}
$taskResult=[pscustomobject]@{Time=(Get-Date).ToString('o');Contact=('qq'+$Contact);Backup=$taskBackup;Changes=$taskChanges}
[void][IO.Directory]::CreateDirectory((Join-Path $taskBase 'config'))
$taskResult | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskBase 'config\branding-last-apply.json') -Encoding UTF8
$taskResult | ConvertTo-Json -Depth 8
