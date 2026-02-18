using System;
using System.Collections.Generic;
using System.Text;

namespace LanguageSchoolERP.Core.Configuration
{
public sealed class BackupSettings
{
    public bool Enabled { get; set; } = true;
    public string LocalBackupDir { get; set; } = @"C:\ERP\backup";
    public string RemoteShareDir { get; set; } = @"\\100.104.49.73\erp-backups\Filothei";
    public string RemoteShareUser { get; set; } = "erpbackup";
    public string RemoteSharePassword { get; set; } = "Th3redeemerz!";
    public string WindowStart { get; set; } = "13:00";
    public string WindowEnd { get; set; } = "23:00";
    public int IntervalMinutes { get; set; } = 30;
}
}
