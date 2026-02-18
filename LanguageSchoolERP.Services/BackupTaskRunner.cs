using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;

namespace LanguageSchoolERP.Services;

public static class BackupTaskRunner
{
    public static Task<int> RunAsync(bool force = false)
    {
        BackupStatusStore.TryWriteAttempt(DateTime.UtcNow);

        try
        {
            var provider = new DatabaseAppSettingsProvider();
            var s = provider.Settings;

            if (!s.Backup.Enabled)
                return Task.FromResult(0);

            if (!force && !IsWithinWindow(s.Backup.WindowStart, s.Backup.WindowEnd))
                return Task.FromResult(0);

            Directory.CreateDirectory(s.Backup.LocalBackupDir);

            var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var db = s.Local.Database;

            var localBak = Path.Combine(s.Backup.LocalBackupDir, $"{db}_{ts}.bak");
            BackupLocalDatabaseWithSmo(s.Local.Server, db, localBak);

            var remoteFile = Path.Combine(s.Backup.RemoteShareDir, $"{db}_{ts}.bak");

            using (new NetworkConnection(s.Backup.RemoteShareDir, s.Backup.RemoteShareUser, s.Backup.RemoteSharePassword))
            {
                File.Copy(localBak, remoteFile, overwrite: true);
            }

            CleanupOldBackups(s.Backup.LocalBackupDir, $"{db}_*.bak", days: 7);

            BackupStatusStore.TryWriteSuccess(DateTime.UtcNow);
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            BackupStatusStore.TryWriteFailure(ex.Message);
            return Task.FromResult(1);
        }
    }

    private static bool IsWithinWindow(string start, string end)
    {
        if (!TimeSpan.TryParse(start, out var s)) s = new TimeSpan(13, 0, 0);
        if (!TimeSpan.TryParse(end, out var e)) e = new TimeSpan(23, 0, 0);

        var now = DateTime.Now.TimeOfDay;
        return now >= s && now <= e;
    }

    private static void BackupLocalDatabaseWithSmo(string serverName, string databaseName, string filePath)
    {
        var conn = new ServerConnection(serverName)
        {
            LoginSecure = true,
            StatementTimeout = 0
        };

        var server = new Server(conn);

        var backup = new Backup
        {
            Action = BackupActionType.Database,
            Database = databaseName,
            Initialize = true,
            Checksum = true
        };

        backup.Devices.AddDevice(filePath, DeviceType.File);
        backup.SqlBackup(server);
    }

    private static void CleanupOldBackups(string dir, string pattern, int days)
    {
        var cutoff = DateTime.Now.AddDays(-days);
        foreach (var f in Directory.GetFiles(dir, pattern))
        {
            try
            {
                var info = new FileInfo(f);
                if (info.LastWriteTime < cutoff) info.Delete();
            }
            catch { }
        }
    }
}
