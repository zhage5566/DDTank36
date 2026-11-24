param([string]$Root=$PSScriptRoot)
$ErrorActionPreference='Stop'
$taskDb=[Data.SqlClient.SqlConnection]::new('Server=(localdb)\DDTank36;Database=Db_Tank_S1;Integrated Security=true')
try{
 $taskDb.Open();$taskTransaction=$taskDb.BeginTransaction();$taskCommand=$taskDb.CreateCommand();$taskCommand.Transaction=$taskTransaction
 $taskCommand.CommandText=@'
UPDATE dbo.Shop SET AValue1=100 WHERE ID=11204703 AND TemplateID=112047 AND APrice1=-4 AND AUnit=1 AND AValue1=999999;
UPDATE dbo.Shop SET AValue1=1000 WHERE ID=11204903 AND TemplateID=112049 AND APrice1=-4 AND AUnit=1 AND AValue1=9999999;
'@
 $taskChanged=$taskCommand.ExecuteNonQuery();$taskTransaction.Commit();Write-Output "Shop price migration: $taskChanged rows corrected; other prices preserved."
}finally{$taskDb.Dispose()}
