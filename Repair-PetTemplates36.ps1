param(
 [string]$Root='D:\DDTank-3.6-Frank-CN',
 [string]$SqlInstance='(localdb)\DDTank36',
 [string]$Database='Db_Tank_S1',
 [string]$TemplateXml,
 [string]$BackupDirectory,
 [switch]$CheckOnly
)
$ErrorActionPreference='Stop'
if(!$TemplateXml){$TemplateXml=Join-Path $Root 'package\DanDanTang\Site\Request\pettemplateinfo.xml'}
if(!$BackupDirectory){$BackupDirectory=Join-Path $Root 'backups\pet-templates36'}
$TemplateXml=[IO.Path]::GetFullPath($TemplateXml)
[xml]$taskXml=[IO.File]::ReadAllText($TemplateXml)
$taskFields=@('LowBloodGrow','LowAttackGrow','LowDefenceGrow','LowAgilityGrow','LowLuckGrow','WashGetCount')
$taskExpected=@{}
foreach($taskNode in $taskXml.SelectNodes('//item')){
 $taskId=0;if(![int]::TryParse($taskNode.GetAttribute('TemplateID'),[ref]$taskId)-or $taskId-le 0){throw 'Invalid XML TemplateID'}
 if($taskExpected.ContainsKey($taskId)){throw "Duplicate XML TemplateID: $taskId"}
 $taskRow=@{};foreach($taskField in $taskFields){$taskValue=0;if(!$taskNode.HasAttribute($taskField)-or ![int]::TryParse($taskNode.GetAttribute($taskField),[ref]$taskValue)){throw "Missing or invalid $taskField for XML template $taskId"};$taskRow[$taskField]=$taskValue}
 $taskExpected[$taskId]=$taskRow
}
if($taskExpected.Count-eq 0){throw 'Pet XML has no templates'}
$taskBuilder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$taskBuilder['Data Source']=$SqlInstance;$taskBuilder['Initial Catalog']=$Database;$taskBuilder['Integrated Security']=$true
$taskConn=New-Object System.Data.SqlClient.SqlConnection($taskBuilder.ConnectionString)
$taskTransaction=$null
function Invoke-PetQuery([string]$Text){$taskQuery=$taskConn.CreateCommand();$taskQuery.CommandTimeout=120;$taskQuery.Transaction=$taskTransaction;$taskQuery.CommandText=$Text;return $taskQuery}
try{
 $taskConn.Open();$taskTransaction=$taskConn.BeginTransaction()
 $taskLock=Invoke-PetQuery "DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource=N'Local36.RepairPetTemplates',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=30000; SELECT @result;"
 if([int]$taskLock.ExecuteScalar()-lt 0){throw 'Could not obtain pet template migration lock'}
 $taskSchema=New-Object System.Data.DataTable
 $taskAdapter=New-Object System.Data.SqlClient.SqlDataAdapter (Invoke-PetQuery "SELECT c.name,t.name AS typeName FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID(N'dbo.Pet_Template')")
 [void]$taskAdapter.Fill($taskSchema)
 if($taskSchema.Rows.Count-eq 0){throw 'dbo.Pet_Template is missing'}
 $taskColumns=@{};foreach($taskColumn in $taskSchema.Rows){$taskColumns[[string]$taskColumn.name]=[string]$taskColumn.typeName}
 foreach($taskField in $taskFields){if($taskColumns.ContainsKey($taskField)-and $taskColumns[$taskField]-ne 'int'){throw "Existing $taskField must be SQL int; refusing implicit conversion"}}
 $taskTable=New-Object System.Data.DataTable 'Pet_Template'
 $taskAdapter=New-Object System.Data.SqlClient.SqlDataAdapter (Invoke-PetQuery 'SELECT * FROM dbo.Pet_Template WITH (TABLOCKX,HOLDLOCK)')
 [void]$taskAdapter.Fill($taskTable)
 $taskMissing=@($taskFields|Where-Object {!$taskColumns.ContainsKey($_)})
 $taskRowsToRepair=0
 foreach($taskRow in $taskTable.Rows){
  $taskId=[int]$taskRow.TemplateID;if(!$taskExpected.ContainsKey($taskId)){throw "DB template $taskId is absent from source XML; no changes made"}
  $taskDifferent=$false;foreach($taskField in $taskFields){if(!$taskColumns.ContainsKey($taskField)-or $taskRow[$taskField]-is [DBNull]-or [int]$taskRow[$taskField]-ne $taskExpected[$taskId][$taskField]){$taskDifferent=$true}}
  if($taskDifferent){$taskRowsToRepair++}
 }
 $taskReport=[ordered]@{database=$Database;table='dbo.Pet_Template';xml=$TemplateXml;xmlSha256=(Get-FileHash -LiteralPath $TemplateXml -Algorithm SHA256).Hash;xmlTemplates=$taskExpected.Count;dbTemplates=$taskTable.Rows.Count;missingColumns=$taskMissing;rowsToRepair=$taskRowsToRepair;checkOnly=[bool]$CheckOnly;changed=$false;backup=$null}
 if($CheckOnly-or ($taskMissing.Count-eq 0-and $taskRowsToRepair-eq 0)){$taskTransaction.Rollback();$taskTransaction=$null;$taskReport|ConvertTo-Json -Depth 5;return}
 # A schema-bearing full-table backup is written before any DDL or UPDATE.
 $taskBackup=Join-Path ([IO.Path]::GetFullPath($BackupDirectory)) ([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffffffZ')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
 [void][IO.Directory]::CreateDirectory($taskBackup)
 $taskTable.WriteXml((Join-Path $taskBackup 'Pet_Template.before.xml'),[System.Data.XmlWriteMode]::WriteSchema)
 $taskProc=Invoke-PetQuery "SELECT OBJECT_DEFINITION(OBJECT_ID(N'dbo.SP_PetTemplateInfo_All'))"
 $taskDefinition=$taskProc.ExecuteScalar();[IO.File]::WriteAllText((Join-Path $taskBackup 'SP_PetTemplateInfo_All.before.sql'),[string]$taskDefinition,[Text.Encoding]::UTF8)
 [IO.File]::Copy($TemplateXml,(Join-Path $taskBackup 'pettemplateinfo.source.xml'))
 $taskReport.backup=$taskBackup
 [IO.File]::WriteAllText((Join-Path $taskBackup 'manifest.json'),($taskReport|ConvertTo-Json -Depth 5),[Text.Encoding]::UTF8)
 foreach($taskField in $taskMissing){$taskDdl=Invoke-PetQuery ("ALTER TABLE dbo.Pet_Template ADD ["+$taskField+"] int NULL");[void]$taskDdl.ExecuteNonQuery()}
 $taskAssignments=($taskFields|ForEach-Object {'['+$_+']=@'+$_})-join ','
 $taskUpdate=Invoke-PetQuery ('UPDATE dbo.Pet_Template SET '+$taskAssignments+' WHERE TemplateID=@TemplateID')
 [void]$taskUpdate.Parameters.Add('@TemplateID',[System.Data.SqlDbType]::Int)
 foreach($taskField in $taskFields){[void]$taskUpdate.Parameters.Add('@'+$taskField,[System.Data.SqlDbType]::Int)}
 foreach($taskRow in $taskTable.Rows){$taskId=[int]$taskRow.TemplateID;$taskUpdate.Parameters['@TemplateID'].Value=$taskId;foreach($taskField in $taskFields){$taskUpdate.Parameters['@'+$taskField].Value=$taskExpected[$taskId][$taskField]};if($taskUpdate.ExecuteNonQuery()-ne 1){throw "Expected one row for template $taskId"}}
 $taskVerify=New-Object System.Data.DataTable
 $taskAdapter=New-Object System.Data.SqlClient.SqlDataAdapter (Invoke-PetQuery ('SELECT TemplateID,'+(($taskFields|ForEach-Object {'['+$_+']'})-join ',')+' FROM dbo.Pet_Template'))
 [void]$taskAdapter.Fill($taskVerify)
 foreach($taskRow in $taskVerify.Rows){foreach($taskField in $taskFields){if($taskRow[$taskField]-is [DBNull]-or [int]$taskRow[$taskField]-ne $taskExpected[[int]$taskRow.TemplateID][$taskField]){throw 'Post-update pet template validation failed'}}}
 $taskTransaction.Commit();$taskTransaction=$null;$taskReport.changed=$true
 [IO.File]::WriteAllText((Join-Path $taskBackup 'applied.json'),($taskReport|ConvertTo-Json -Depth 5),[Text.Encoding]::UTF8)
 $taskReport|ConvertTo-Json -Depth 5
}finally{if($taskTransaction){$taskTransaction.Rollback();$taskTransaction.Dispose()};$taskConn.Dispose()}
