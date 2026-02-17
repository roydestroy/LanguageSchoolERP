param(
  [Parameter(Mandatory=$true)]
  [string]$Config
)

$ErrorActionPreference = "Stop"
$c = Get-Content $Config -Raw | ConvertFrom-Json

$School = $c.School
$DbName = $c.DbName
$Instance = $c.Instance
$LocalBackupDir = $c.LocalBackupDir
$RemoteShare = $c.RemoteShare

$Date = Get-Date -Format "yyyy-MM-dd_HH-mm"
$bakLocal = Join-Path $LocalBackupDir "${DbName}_${Date}.bak"
$bakRemoteLatest = Join-Path $RemoteShare "${DbName}_latest.bak"

New-Item -ItemType Directory -Force -Path $LocalBackupDir | Out-Null

# Backup (SQL Express: no COMPRESSION)
$sql = "BACKUP DATABASE [$DbName] TO DISK = N'$bakLocal' WITH INIT;"

& sqlcmd -S $Instance -E -b -Q $sql

# Copy to server room
New-Item -ItemType Directory -Force -Path $RemoteShare | Out-Null
Copy-Item -Force $bakLocal $bakRemoteLatest

# Retention locally: keep 7 days (tune)
Get-ChildItem $LocalBackupDir -Filter "${DbName}_*.bak" |
  Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-7) } |
  Remove-Item -Force
