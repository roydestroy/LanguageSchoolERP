$server = ".\SQLEXPRESS"
$db = "FilotheiSchoolERP"
$backupFile = "C:\ERP\backup\$db.bak"

$sharePath = "\\100.104.49.73\erp-backups\Filothei"
$remoteFile = "$sharePath\${db}_latest.bak"

$sqlcmd = "sqlcmd"

Write-Host "Creating backup..."

$sql = "BACKUP DATABASE [$db] TO DISK='$backupFile' WITH INIT"
& $sqlcmd -S $server -E -C -Q $sql

Write-Host "Uploading backup..."
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$remoteTemp = Join-Path $sharePath "${db}_${timestamp}.bak"
Copy-Item $backupFile $remoteTemp -Force


Write-Host "Done."
