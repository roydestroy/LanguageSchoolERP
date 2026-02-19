using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;

namespace LanguageSchoolERP.Services;

public static class BackupTaskRunner
{
    public static Task<int> RunAsync(bool force = false, string? localDatabaseName = null)
    {
        string? db = null;

        try
        {
            var provider = new DatabaseAppSettingsProvider();
            var s = provider.Settings;

            db = string.IsNullOrWhiteSpace(localDatabaseName)
                ? s.Local.Database
                : localDatabaseName.Trim();

            BackupStatusStore.TryWriteAttempt(DateTime.UtcNow, db);

            if (!s.Backup.Enabled)
                return Task.FromResult(0);

            if (!force && !IsWithinWindow(s.Backup.WindowStart, s.Backup.WindowEnd))
                return Task.FromResult(0);

            Directory.CreateDirectory(s.Backup.LocalBackupDir);

            var shareConnectivity = RemoteConnectivityDiagnostics.CheckRemoteShare(
                s.Backup.RemoteShareRoot,
                s.Backup.RemoteShareUser,
                s.Backup.RemoteSharePassword);

            if (!shareConnectivity.IsSuccess)
            {
                var diagnostics = string.IsNullOrWhiteSpace(shareConnectivity.Details)
                    ? shareConnectivity.UserMessage ?? "remote share unavailable"
                    : $"{shareConnectivity.UserMessage}: {shareConnectivity.Details}";

                BackupStatusStore.TryWriteFailure(diagnostics, db);
                return Task.FromResult(1);
            }

            var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            var localBak = Path.Combine(s.Backup.LocalBackupDir, $"{db}_{ts}.bak");
            BackupLocalDatabaseWithSmo(s.Local.Server, db, localBak);

            var branchFolders = s.Backup.RemoteBranchFolders ?? [];
            if (branchFolders.Count == 0)
            {
                BackupStatusStore.TryWriteFailure("No remote branch folders configured.", db);
                return Task.FromResult(1);
            }

            using (new NetworkConnection(s.Backup.RemoteShareRoot, s.Backup.RemoteShareUser, s.Backup.RemoteSharePassword))
            {
                foreach (var branch in branchFolders)
                {
                    if (string.IsNullOrWhiteSpace(branch))
                        continue;

                    var remoteDir = $@"{s.Backup.RemoteShareRoot}\{branch.Trim()}";
                    Directory.CreateDirectory(remoteDir);

                    var remoteFile = Path.Combine(remoteDir, $"{db}_{ts}.bak");
                    File.Copy(localBak, remoteFile, overwrite: true);
                }
            }

            CleanupOldBackups(s.Backup.LocalBackupDir, $"{db}_*.bak", days: 7);

            BackupStatusStore.TryWriteSuccess(DateTime.UtcNow, db);
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            BackupStatusStore.TryWriteFailure(ex.Message, db);
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
