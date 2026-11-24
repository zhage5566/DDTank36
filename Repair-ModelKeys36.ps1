param([string]$Root=$PSScriptRoot)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Security
$taskDb=[Data.SqlClient.SqlConnection]::new('Server=(localdb)\DDTank36;Database=Db_Tank_S1;Integrated Security=true');$taskDb.Open()
try{
 $taskCmd=$taskDb.CreateCommand();$taskCmd.CommandText="IF OBJECT_ID('dbo.Local36_BotLlm') IS NULL SELECT CONVERT(int,0) ELSE SELECT CONVERT(int,1)"
 if([int]$taskCmd.ExecuteScalar() -eq 0){return}
 $taskCmd.CommandText='SELECT UserID,ApiKeyEncrypted,SecretEncrypted FROM Local36_BotLlm';$taskTable=[Data.DataTable]::new();$taskTable.Load($taskCmd.ExecuteReader())
 $taskInvalid=@();$taskEntropy=[Text.Encoding]::UTF8.GetBytes('DDTank36.LocalBot.Llm.v1')
 foreach($taskRow in $taskTable.Rows){$taskBad=$false;foreach($taskColumn in @('ApiKeyEncrypted','SecretEncrypted')){if($taskRow[$taskColumn] -is [DBNull]){continue};try{$taskPlain=[Security.Cryptography.ProtectedData]::Unprotect([byte[]]$taskRow[$taskColumn],$taskEntropy,[Security.Cryptography.DataProtectionScope]::LocalMachine);[Array]::Clear($taskPlain,0,$taskPlain.Length)}catch [Security.Cryptography.CryptographicException]{$taskBad=$true}}
  if($taskBad){
   $taskApiBlob=if($taskRow.ApiKeyEncrypted -is [DBNull]){$null}else{[Convert]::ToBase64String($taskRow.ApiKeyEncrypted)}
   $taskSecretBlob=if($taskRow.SecretEncrypted -is [DBNull]){$null}else{[Convert]::ToBase64String($taskRow.SecretEncrypted)}
   $taskInvalid+=@([pscustomobject]@{id=[int]$taskRow.UserID;apiKeyEncrypted=$taskApiBlob;secretEncrypted=$taskSecretBlob})
  }
 }
 if($taskInvalid.Count){$taskBackup=Join-Path $Root ('config\migration-keys-'+(Get-Date -Format yyyyMMdd-HHmmss)+'.json');$taskInvalid | ConvertTo-Json | Set-Content -LiteralPath $taskBackup -Encoding UTF8
  foreach($taskRow in $taskInvalid){$taskCmd=$taskDb.CreateCommand();$taskCmd.CommandText='UPDATE Local36_BotLlm SET Enabled=0,ApiKeyEncrypted=NULL,SecretEncrypted=NULL WHERE UserID=@id';[void]$taskCmd.Parameters.AddWithValue('@id',$taskRow.id);[void]$taskCmd.ExecuteNonQuery()}
  Write-Output ('Model keys from another Windows machine were disabled for '+$taskInvalid.Count+' NPCs. Re-enter them in GM; personas and memory were preserved.')
 }else{Write-Output 'Existing model keys are valid for this Windows machine; preserved.'}
}finally{$taskDb.Dispose()}
