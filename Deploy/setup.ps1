# setup.ps1  (RUN AS ADMIN)

param(
  [Parameter(Mandatory=$true)]
  [ValidateSet("Filothei","NeaIonia")]
  [string]$School
)

$ErrorActionPreference = "Stop"

$InstanceName = "SQLEXPRESS"
$SqlServiceName = "MSSQL`$$InstanceName"
$DbName = if ($School -eq "Filothei") { "FilotheiSchoolERP" } else { "NeaIoniaSchoolERP" }

# Where the installer placed the payload
$DeployDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$BakPath = Join-Path $DeployDir ("restore-" + $School.ToLower() + ".bak")

# Local backup dir
$LocalBackupDir = "C:\ERPBackups"
New-Item -ItemType Directory -Force -Path $LocalBackupDir | Out-Null

Write-Host "=== 1) Ensure SQL Server Express is installed ==="
# We assume your bootstrapper already installed SQL Express if missing.
# Here we just verify the service exists:
$svc = Get-Service -Name $SqlServiceName -ErrorAction SilentlyContinue
if (-not $svc) {
  throw "SQL Server Express instance $InstanceName not found. Ensure the installer includes SQL Express and installs it first."
}

Write-Host "=== 2) Enable TCP/IP and set fixed port 1433 ==="

# Find instance registry key (e.g. MSSQL16.SQLEXPRESS)
$instKey = "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL"
$instanceId = (Get-ItemProperty $instKey).$InstanceName
if (-not $instanceId) { throw "Could not locate instance id for $InstanceName." }

$tcpIpAll = "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\$instanceId\MSSQLServer\SuperSocketNetLib\Tcp\IPAll"
$tcpProto = "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\$instanceId\MSSQLServer\SuperSocketNetLib\Tcp"

# Enable TCP protocol
Set-ItemProperty -Path $tcpProto -Name "Enabled" -Value 1 -Type DWord

# Set fixed port 1433 (clear dynamic ports)
Set-ItemProperty -Path $tcpIpAll -Name "TcpDynamicPorts" -Value "" -Type String
Set-ItemProperty -Path $tcpIpAll -Name "TcpPort" -Value "1433" -Type String

Write-Host "=== 3) Open Windows Firewall port 1433 ==="
$ruleName = "LanguageSchoolERP - SQL Server 1433"
if (-not (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)) {
  New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Protocol TCP -LocalPort 1433 -Action Allow | Out-Null
}

Write-Host "=== 4) Restart SQL service ==="
Restart-Service -Name $SqlServiceName -Force
Start-Sleep -Seconds 3

Write-Host "=== 5) Restore database locally (if not exists) ==="
# Use Windows Auth locally. (SQL Express installed with Windows auth by default.)
# We'll restore using sqlcmd. Ensure sqlcmd exists (it usually does with SQL Express or you can bundle SQLCMD tools).
$sqlcmd = "sqlcmd.exe"
if (-not (Get-Command $sqlcmd -ErrorAction SilentlyContinue)) {
  throw "sqlcmd.exe not found. Install SQL Server Command Line Utilities or bundle them."
}

# Find default data folder for the instance (read master file path from Parameters)
$paramKey = "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\$instanceId\MSSQLServer\Parameters"
$params = Get-ItemProperty $paramKey
$masterData = ($params.PSObject.Properties | Where-Object { $_.Name -like "SQLArg*" } | ForEach-Object { $_.Value } | Where-Object { $_ -like "-d*" } | Select-Object -First 1)
if (-not $masterData) { throw "Could not locate master data path." }
$masterDataPath = $masterData.Substring(2) # remove -d
$dataDir = Split-Path -Parent $masterDataPath

$mdf = Join-Path $dataDir ($DbName + ".mdf")
$ldf = Join-Path $dataDir ($DbName + "_log.ldf")

# Only restore if DB doesn't exist
$dbExists = & $sqlcmd -S ".\$InstanceName" -E -h -1 -W -Q "SET NOCOUNT ON; SELECT DB_ID(N'$DbName');"
if ($dbExists -match "NULL" -or [string]::IsNullOrWhiteSpace($dbExists)) {
  if (-not (Test-Path $BakPath)) { throw "Backup file not found: $BakPath" }

  $restore = @"
RESTORE DATABASE [$DbName]
FROM DISK = N'$BakPath'
WITH MOVE N'${DbName}' TO N'$mdf',
     MOVE N'${DbName}_log' TO N'$ldf',
     REPLACE;
"@

  & $sqlcmd -S ".\$InstanceName" -E -b -Q $restore
} else {
  Write-Host "Database $DbName already exists. Skipping restore."
}

Write-Host "=== Setup complete. ==="
Write-Host "Local DB: .\SQLEXPRESS / $DbName"
Write-Host "LAN clients connect to: <HOSTNAME>\SQLEXPRESS (or <IP>\SQLEXPRESS)"
