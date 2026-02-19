using System.Collections.Generic;

namespace LanguageSchoolERP.Core.Configuration
{
public sealed class BackupSettings
{
    public bool Enabled { get; set; } = true;
    public string LocalBackupDir { get; set; } = @"C:\ERP\backup";

    // Root share (no branch here)
    public string RemoteShareRoot { get; set; } = @"\\100.104.49.73\erp-backups";

    public string RemoteShareUser { get; set; } = "erpbackup";
    public string RemoteSharePassword { get; set; } = "";

    // Which branch folders to write to (both from same machine)
    public List<string> RemoteBranchFolders { get; set; } = new()
    {
        "Filothei",
        "NeaIonia"
    };

    public string WindowStart { get; set; } = "13:00";
    public string WindowEnd { get; set; } = "23:00";
    public int IntervalMinutes { get; set; } = 30;
}
}
