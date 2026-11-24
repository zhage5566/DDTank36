param([string]$Root=$PSScriptRoot)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath($Root)
$taskPackage=Join-Path $taskRoot 'package\DanDanTang'
$taskSite=Join-Path $taskPackage 'Site'
$taskRoad=Join-Path $taskPackage 'Server\12313\Road\Road'
$taskHttp='http://127.0.0.1:8342'
$taskBackup=Join-Path $taskRoot ('config\configure-backup\'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$taskXmlWriter=[Xml.XmlWriterSettings]::new();$taskXmlWriter.Indent=$true;$taskXmlWriter.Encoding=[Text.UTF8Encoding]::new($false)
function Save-Config36([Xml.XmlDocument]$Document,[string]$Path){
 if($Document.DocumentElement.LocalName -eq 'configuration'){
  if($Document.DocumentElement.NamespaceURI){throw "configuration root has an unexpected namespace: $Path"}
  # XmlDocument does not hoist namespace declarations onto configuration, unlike
  # ElementTree serialization. Repair old asm.v1 declarations only when found.
  foreach($taskAttribute in @($Document.DocumentElement.Attributes)){
   if($taskAttribute.Value -eq 'urn:schemas-microsoft-com:asm.v1' -and ($taskAttribute.Prefix -eq 'xmlns' -or $taskAttribute.Name -eq 'xmlns')){[void]$Document.DocumentElement.RemoveAttributeNode($taskAttribute)}
  }
  foreach($taskRuntime in @($Document.SelectNodes('/configuration/runtime'))){
   $taskBindings=@($taskRuntime.SelectNodes('*[local-name()="assemblyBinding"]'))
   if($taskBindings.Count){
    $taskMerged=$Document.CreateElement('assemblyBinding','urn:schemas-microsoft-com:asm.v1')
    foreach($taskBinding in $taskBindings){
     foreach($taskChild in @($taskBinding.ChildNodes)){
      if($taskChild.NodeType -ne [Xml.XmlNodeType]::Element){continue}
      $taskFragment=[Xml.XmlDocument]::new();$taskFragment.LoadXml(($taskChild.OuterXml -replace '<(/?)(?:[A-Za-z_][\w.-]*:)?(\w+)', '<$1$2'))
      function Copy-AssemblyNode36([Xml.XmlNode]$Node){
       $taskNew=$Document.CreateElement($Node.LocalName,'urn:schemas-microsoft-com:asm.v1')
       foreach($taskAttr in $Node.Attributes){if($taskAttr.Prefix -ne 'xmlns' -and $taskAttr.Name -ne 'xmlns'){$taskNew.SetAttribute($taskAttr.LocalName,$taskAttr.Value)}}
       foreach($taskNested in $Node.ChildNodes){if($taskNested.NodeType -eq [Xml.XmlNodeType]::Element){[void]$taskNew.AppendChild((Copy-AssemblyNode36 $taskNested))}elseif($taskNested.NodeType -eq [Xml.XmlNodeType]::Text){[void]$taskNew.AppendChild($Document.CreateTextNode($taskNested.Value))}}
       return ,$taskNew
      }
      [void]$taskMerged.AppendChild((Copy-AssemblyNode36 $taskFragment.DocumentElement))
     }
     [void]$taskRuntime.RemoveChild($taskBinding)
    }
    [void]$taskRuntime.AppendChild($taskMerged)
   }
  }
 }
 [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))
 $taskWriter=[Xml.XmlWriter]::Create($Path,$taskXmlWriter)
 try{$Document.Save($taskWriter)}finally{$taskWriter.Dispose()}
 # Parse the written artifact as a separate verification step.
 $taskVerify=[Xml.XmlDocument]::new();$taskVerify.Load($Path)
}
function Backup-Config36([string]$Path){
 if(!(Test-Path -LiteralPath $Path)){return}
 $taskRel=$Path.Substring($taskRoot.Length).TrimStart('\')
 $taskDest=Join-Path $taskBackup $taskRel
 [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskDest))
 if(!(Test-Path -LiteralPath $taskDest)){Copy-Item -LiteralPath $Path -Destination $taskDest -Force}
}
function Database-Connection36([string]$Value){
 $taskMatch=[regex]::Match($Value,'(?i)(?:Initial\s+Catalog|Database)\s*=\s*["'']?([^;"'']+)')
 if(!$taskMatch.Success){return $Value}
 $taskName=$taskMatch.Groups[1].Value.Trim()
 if($taskName -eq 'Db_Tank'){$taskName='Db_Tank_S1'}
 if($taskName -notin @('Db_Tank_S1','Db_Membership','Db_Count','Db_Cross','Db_Logs')){throw "Unexpected database name in original package configuration: $taskName"}
 return ('Data Source=(localdb)\DDTank36;Initial Catalog='+$taskName+';Integrated Security=True;MultipleActiveResultSets=True')
}
if(!(Test-Path -LiteralPath $taskRoad)){throw "Original 3.6 package not found: $taskRoad"}
$taskDirectories=@(
 $taskRoad,(Join-Path $taskRoad 'Request'),(Join-Path $taskRoad 'Request\bin'),
 (Join-Path $taskPackage 'Server\Center'),(Join-Path $taskPackage 'Server\Fight'),(Join-Path $taskPackage 'Server\World.Fight'),
 (Join-Path $taskSite 'Register'),(Join-Path $taskSite 'Register\bin'),
 (Join-Path $taskSite 'Flash'),(Join-Path $taskSite 'Flash\bin'),
 (Join-Path $taskSite 'Request'),(Join-Path $taskSite 'Request\bin')
)
$taskFiles=@($taskDirectories | Where-Object {Test-Path -LiteralPath $_} | ForEach-Object {Get-ChildItem -LiteralPath $_ -File -Filter '*.config'})
$taskUpdated=@()
foreach($taskFile in $taskFiles){
 $taskPath=$taskFile.FullName;Backup-Config36 $taskPath
 $taskDoc=[Xml.XmlDocument]::new();$taskDoc.Load($taskPath)
 foreach($taskAdd in @($taskDoc.SelectNodes('/configuration/appSettings/add'))){
  $taskKey=$taskAdd.GetAttribute('key');$taskValue=$taskAdd.GetAttribute('value');$taskLower=$taskKey.ToLowerInvariant()
  # Two original DLL sidecars contain conCrossString=Db_Player. They are not the
  # ASP.NET application's Web.config. No Db_Player backup exists in this package;
  # preserve this unmatched legacy setting rather than guess a database mapping.
  if($taskFile.Name -eq 'Tank.Request.dll.config' -and $taskLower -eq 'concrossstring' -and $taskValue -match '(?i)(?:Initial\s+Catalog|Database)\s*=\s*Db_Player(?:;|$)'){continue}
  if($taskValue -match '(?i)(Initial\s+Catalog|Database)\s*='){$taskValue=Database-Connection36 $taskValue}
  if($taskValue -match '^https?://(?:192\.168\.1\.202|192\.168\.0\.4|27\.25\.157\.100|127\.0\.0\.1):(?:85|840|9001|8342)(?:/|$)'){$taskValue=[regex]::Replace($taskValue,'^https?://[^/]+',$taskHttp)}
  switch($taskLower){
   {$_ -in @('ip','ipaddress','loginserverip','fightserverip','adminip','sentrewardip')} {$taskValue='127.0.0.1'}
   'port' {if($taskPath -match '\\Road\\Road\\Road\.(Service|Local)\.exe\.config$'){$taskValue='9400'}elseif($taskPath -match '\\Center\\Center\.Service\.exe\.config$'){$taskValue='9402'}elseif($taskPath -match '\\World\.Fight\\'){$taskValue='9409'}elseif($taskPath -match '\\Fight\\'){$taskValue='9408'}}
   'loginserverport' {$taskValue='9402'}
   'fightserverport' {$taskValue='9408'}
   'reqpath' {$taskValue=Join-Path $taskSite 'Request'}
   'requesturl' {$taskValue=$taskHttp+'/Request/'}
   'templatedir' {$taskValue=Join-Path $taskSite 'Register\webfile1\vm'}
   'webfileserver' {$taskValue=$taskHttp+'/webfile1'}
   'webgameserver' {$taskValue=$taskHttp+'/Register/UserLogin.aspx'}
   'loginaddress' {$taskValue=$taskHttp+'/Register/login.aspx'}
   'logoutaddress' {$taskValue=$taskHttp+'/Register/logout.aspx'}
   'createlogin' {$taskValue=$taskHttp+'/Request/CreateLogin.aspx'}
   'loginreturn' {$taskValue=$taskHttp+'/Flash/Index.aspx'}
   'loginserver' {$taskValue=$taskHttp+'/Register/Flash_Port/PassPort.asmx'}
   'loginonurl' {$taskValue=$taskHttp+'/Register/login.aspx'}
   'flashurl' {$taskValue=$taskHttp+'/Flash/Index.aspx'}
   'serverid' {if($taskPath -match '\\Request\\'){$taskValue='1001'}}
   'loginurl' {$taskValue=$taskHttp+'/Request/CreateLogin.aspx'}
   'logpath' {if([IO.Path]::IsPathRooted($taskValue)){$taskValue=Join-Path $taskRoot 'logs\Request'}}
  }
  $taskAdd.SetAttribute('value',$taskValue)
 }
 foreach($taskConnection in @($taskDoc.SelectNodes('/configuration/connectionStrings/add[@connectionString]'))){
  $taskConnection.SetAttribute('connectionString',(Database-Connection36 $taskConnection.GetAttribute('connectionString')))
 }
 # Only WCF endpoint/base-address fields are rewritten; signing keys are untouched.
 foreach($taskAttribute in @($taskDoc.SelectNodes('/configuration/system.serviceModel//@address | /configuration/system.serviceModel//@baseAddress'))){
  if($taskAttribute.Value -match '^net\.tcp://'){$taskAttribute.Value='net.tcp://127.0.0.1:9488/'}
  elseif($taskAttribute.Value -match '^http://[^/]+:(?:2008|2408)/'){$taskAttribute.Value=[regex]::Replace($taskAttribute.Value,'^http://[^/]+','http://127.0.0.1:2408')}
 }
 foreach($taskNode in @($taskDoc.SelectNodes('/configuration/system.web/sessionState'))){$taskNode.SetAttribute('mode','InProc')}
 foreach($taskNode in @($taskDoc.SelectNodes('/configuration/system.web/authentication'))){$taskNode.SetAttribute('mode','None')}
 foreach($taskNode in @($taskDoc.SelectNodes('/configuration/system.web/compilation'))){$taskNode.SetAttribute('targetFramework','4.7.2')}
 if($taskFile.Name -match '\.exe\.config$'){
  $taskStartup=$taskDoc.SelectSingleNode('/configuration/startup');if(!$taskStartup){$taskStartup=$taskDoc.CreateElement('startup');[void]$taskDoc.DocumentElement.AppendChild($taskStartup)}
  foreach($taskNode in @($taskStartup.SelectNodes('supportedRuntime'))){[void]$taskStartup.RemoveChild($taskNode)}
  $taskSupported=$taskDoc.CreateElement('supportedRuntime');$taskSupported.SetAttribute('version','v4.0');$taskSupported.SetAttribute('sku','.NETFramework,Version=v4.7.2');[void]$taskStartup.AppendChild($taskSupported)
 }
 Save-Config36 $taskDoc $taskPath;$taskUpdated+=$taskPath.Substring($taskRoot.Length+1)
}
# Road reads battle.xml independently of appSettings. Preserve each server's
# original authentication key while making both transport endpoints local.
$taskBattlePath=Join-Path $taskRoad 'battle.xml'
if(!(Test-Path -LiteralPath $taskBattlePath)){throw "Missing original battle endpoint list: $taskBattlePath"}
Backup-Config36 $taskBattlePath
$taskBattle=[Xml.XmlDocument]::new();$taskBattle.Load($taskBattlePath)
foreach($taskID in @('1','2')){
 $taskBattleNode=$taskBattle.SelectSingleNode('/list/server[@id="'+$taskID+'"]')
 if(!$taskBattleNode){throw "Expected original battle server id $taskID in battle.xml"}
 $taskBattleNode.SetAttribute('ip','127.0.0.1')
 $taskBattleNode.SetAttribute('port',$(if($taskID -eq '1'){'9408'}else{'9409'}))
}
Save-Config36 $taskBattle $taskBattlePath
$taskUpdated+=$taskBattlePath.Substring($taskRoot.Length+1)
# The original archive does not contain the local executable hosts; their CLR
# configuration must follow the configured original native-server settings.
foreach($taskPair in @(
 @((Join-Path $taskPackage 'Server\Center\Center.Service.exe.config'),(Join-Path $taskPackage 'Server\Center\Center.Local.exe.config')),
 @((Join-Path $taskRoad 'Road.Service.exe.config'),(Join-Path $taskRoad 'Road.Local.exe.config')),
 @((Join-Path $taskPackage 'Server\Fight\Fighting.Service.exe.config'),(Join-Path $taskPackage 'Server\Fight\Fight.Local.exe.config')),
 @((Join-Path $taskPackage 'Server\World.Fight\Fighting.Service.exe.config'),(Join-Path $taskPackage 'Server\World.Fight\Fight.Local.exe.config'))
)){
 if(Test-Path -LiteralPath $taskPair[0]){Backup-Config36 $taskPair[1];Copy-Item -LiteralPath $taskPair[0] -Destination $taskPair[1] -Force}
}
[void][IO.Directory]::CreateDirectory((Join-Path $taskRoot 'logs\Request'))
# The root and Resource applications intentionally have only static configuration.
foreach($taskPair in @(
 @((Join-Path $taskSite 'Web.config'),'<?xml version="1.0" encoding="utf-8"?><configuration><system.webServer><defaultDocument><files><clear/><add value="index.html"/></files></defaultDocument></system.webServer></configuration>'),
 @((Join-Path $taskSite 'Resource\web.config'),'<?xml version="1.0" encoding="utf-8"?><configuration><system.webServer><staticContent><remove fileExtension=".ui"/><mimeMap fileExtension=".ui" mimeType="application/octet-stream"/><remove fileExtension=".swf"/><mimeMap fileExtension=".swf" mimeType="application/x-shockwave-flash"/></staticContent></system.webServer></configuration>')
)){
 Backup-Config36 $taskPair[0];$taskStatic=[Xml.XmlDocument]::new();$taskStatic.LoadXml($taskPair[1]);Save-Config36 $taskStatic $taskPair[0]
}
$taskFlashConfig=Join-Path $taskSite 'Flash\config.xml';Backup-Config36 $taskFlashConfig
$taskDoc=[Xml.XmlDocument]::new();$taskDoc.Load($taskFlashConfig)
$taskFlashUrls=@{FLASHSITE='/Resource/flash/';BACKUP_FLASHSITE='/Resource/flash1/';SITE='/Resource/';FIRSTPAGE='/Register/login.aspx';REGISTER='/Register/reg.aspx';REQUEST_PATH='/Request/';LOGIN_PATH='/Register/UserLogin.aspx';FILL_PATH='/Portal/recharge.html'}
foreach($taskName in $taskFlashUrls.Keys){$taskNode=$taskDoc.SelectSingleNode('/root/config/'+$taskName);if($taskNode){$taskNode.SetAttribute('value',($taskHttp+$taskFlashUrls[$taskName]))}}
foreach($taskNode in @($taskDoc.SelectNodes('/root/config/POLICY_FILES/file'))){$taskNode.SetAttribute('value',($taskHttp+'/Flash/crossdomain.xml'))}
$taskContact=$taskDoc.SelectSingleNode('/root/config/QQ_CS');if($taskContact){$taskContact.SetAttribute('value','961077680')}
Save-Config36 $taskDoc $taskFlashConfig
$taskResourceConfig=Join-Path $taskSite 'Resource\flash\config.xml';Backup-Config36 $taskResourceConfig;Copy-Item -LiteralPath $taskFlashConfig -Destination $taskResourceConfig -Force
# Entry aliases are generated explicitly; existing GM settings are never opened.
$taskEntryMap=@{
 'index.html'='<!doctype html><meta charset="utf-8"><meta http-equiv="refresh" content="0;url=/Register/UserLogin.aspx"><a href="/Register/UserLogin.aspx">Enter DDTank 3.6</a>'
 'Register\login.aspx'='<%@ Page Language="C#" %><% Response.Redirect("UserLogin.aspx"); %>'
 'UserLogin.aspx'='<%@ Page Language="C#" %><% Response.Redirect("/Register/UserLogin.aspx"); %>'
 'Reg.aspx'='<%@ Page Language="C#" %><% Response.Redirect("/Register/Reg.aspx"); %>'
}
foreach($taskRelative in $taskEntryMap.Keys){$taskPath=Join-Path $taskSite $taskRelative;Backup-Config36 $taskPath;[IO.File]::WriteAllText($taskPath,$taskEntryMap[$taskRelative],[Text.UTF8Encoding]::new($false))}
$taskTemplate=Join-Path $PSScriptRoot 'applicationhost.template.config';if(!(Test-Path -LiteralPath $taskTemplate)){throw 'Missing sanitized applicationhost.template.config beside configuration script.'}
$taskIisText=[IO.File]::ReadAllText($taskTemplate).Replace('__ROOT__',[Security.SecurityElement]::Escape($taskRoot))
$taskIisDoc=[Xml.XmlDocument]::new();$taskIisDoc.LoadXml($taskIisText)
$taskIisOutput=Join-Path $taskRoot 'config\applicationhost.config';Backup-Config36 $taskIisOutput;Save-Config36 $taskIisDoc $taskIisOutput
[pscustomobject]@{Time=(Get-Date).ToString('o');ConfigurationFiles=$taskUpdated;Http=$taskHttp;Backup=$taskBackup} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskRoot 'config\configure-last-apply.json') -Encoding UTF8
Write-Output ('Configured original 3.6 package and isolated IIS site: '+$taskHttp)
