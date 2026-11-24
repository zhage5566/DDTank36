param([string]$Root='D:\DDTank-3.6-Frank-CN',[string]$ConnectionString='Server=(localdb)\DDTank36;Database=Db_Tank_S1;Integrated Security=true',[string]$Contact='qq961077680')
$ErrorActionPreference='Stop'
if($Contact -notmatch '^qq\d{5,12}$'){throw 'Expected qq followed by QQ contact digits.'}
$taskTargets=@(
 @{Table='Quest';Key='ID';Columns=@('Title','Detail','Objective')},
 @{Table='Cloth_Property';Key='ID';Columns=@('Name')},
 @{Table='New_Title';Key='ID';Columns=@('Name')},
 @{Table='Shop_Goods';Key='TemplateID';Columns=@('Name','Description')},
 @{Table='Shop_Goods_3';Key='TemplateID';Columns=@('Name','Description')}
)
$taskDb=[Data.SqlClient.SqlConnection]::new($ConnectionString);$taskDb.Open()
try {
 $taskPlanned=@()
 foreach($taskTarget in $taskTargets){
  foreach($taskColumn in $taskTarget.Columns){
   $taskCmd=$taskDb.CreateCommand();$taskCmd.CommandText='SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(@table) AND name=@column'
   [void]$taskCmd.Parameters.AddWithValue('@table',$taskTarget.Table);[void]$taskCmd.Parameters.AddWithValue('@column',$taskColumn)
   if([int]$taskCmd.ExecuteScalar() -eq 0){continue}
   $taskCmd.Parameters.Clear();$taskCmd.CommandText="SELECT [$($taskTarget.Key)],CONVERT(nvarchar(max),[$taskColumn]) FROM [$($taskTarget.Table)] WHERE CONVERT(nvarchar(max),[$taskColumn]) LIKE N'%璀璨%'"
   $taskTable=[Data.DataTable]::new();$taskAdapter=[Data.SqlClient.SqlDataAdapter]::new($taskCmd);[void]$taskAdapter.Fill($taskTable)
   foreach($taskRow in $taskTable.Rows){
    $taskBefore=[string]$taskRow[1]
    $taskAfter=$taskBefore.Replace('璀璨星辰弹弹堂',$Contact).Replace('璀璨弹弹堂',$Contact).Replace('璀璨星辰',$Contact)
    $taskAfter=[regex]::Replace($taskAfter,'(装备[1-8]-)璀璨',('${1}'+$Contact))
    if($taskAfter -eq $taskBefore){continue}
    $taskPlanned+=[pscustomobject]@{Table=$taskTarget.Table;KeyColumn=$taskTarget.Key;ID=[int]$taskRow[0];Column=$taskColumn;Before=$taskBefore;After=$taskAfter}
   }
  }
 }
 $taskBackup=Join-Path $Root ('config\branding-backup\database-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.json')
 [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskBackup))
 [pscustomobject]@{Time=(Get-Date).ToString('o');Scope='Public item/quest/title templates only; no player records';Changes=$taskPlanned} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $taskBackup -Encoding UTF8
 $taskTransaction=$taskDb.BeginTransaction()
 try{
  foreach($taskChange in $taskPlanned){
   $taskCmd=$taskDb.CreateCommand();$taskCmd.Transaction=$taskTransaction
   $taskCmd.CommandText="UPDATE [$($taskChange.Table)] SET [$($taskChange.Column)]=@after WHERE [$($taskChange.KeyColumn)]=@id AND CONVERT(nvarchar(max),[$($taskChange.Column)])=@before"
   [void]$taskCmd.Parameters.AddWithValue('@after',$taskChange.After);[void]$taskCmd.Parameters.AddWithValue('@id',$taskChange.ID);[void]$taskCmd.Parameters.AddWithValue('@before',$taskChange.Before)
   if($taskCmd.ExecuteNonQuery() -ne 1){throw 'Branding row changed concurrently; transaction rolled back.'}
  }
  $taskTransaction.Commit()
 }catch{$taskTransaction.Rollback();throw}
 [pscustomobject]@{UpdatedFields=$taskPlanned.Count;Backup=$taskBackup;Contact=$Contact} | ConvertTo-Json
}finally{$taskDb.Dispose()}
