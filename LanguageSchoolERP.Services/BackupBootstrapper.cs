using System.Diagnostics;
using System.Security.Principal;
using LanguageSchoolERP.Services; // settings provider namespace

public static class BackupBootstrapper
{
    private const string TaskName = "LanguageSchoolERP Backup Upload";

    public static async Task TryBootstrapAsync()
    {
        var provider = new DatabaseAppSettingsProvider();
        var s = provider.Settings;

        if (!s.Backup.Enabled) return;

        // Needs admin once to create scheduled task + SQL login for SYSTEM + folder ACL
        if (!IsAdministrator())
            return; // optional: show UI message "Run once as Administrator to enable backups."

        Directory.CreateDirectory(@"C:\ERP");
        Directory.CreateDirectory(@"C:\ERP\backup");

        // Ensure SQL Server service account can write backups to C:\ERP\backup
        // SQL Express service: NT SERVICE\MSSQL$SQLEXPRESS
        Run("icacls", @"C:\ERP\backup /grant ""NT SERVICE\MSSQL$SQLEXPRESS"":(OI)(CI)M /T");

        // Ensure NT AUTHORITY\SYSTEM is sysadmin so scheduled task can backup locally
        EnsureSqlSystemLoginIsSysAdmin(s.Local.Server);

        // Create/Update scheduled task to run every 30 minutes (guarded by time window in code)
        EnsureScheduledTask();
        await Task.CompletedTask;
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void EnsureScheduledTask()
    {
        var exePath = Process.GetCurrentProcess().MainModule?.FileName
                      ?? throw new InvalidOperationException("Cannot determine exe path.");

        // schtasks quoting is picky:
        var tr = $"\"{exePath}\" --run-backup";

        Run("schtasks",
            $"/Create /F /RU SYSTEM /SC MINUTE /MO 30 /TN \"{TaskName}\" /TR \"{tr}\"");
    }

    private static void EnsureSqlSystemLoginIsSysAdmin(string server)
    {
        // Use integrated security as the admin user who is running the app during bootstrap
        // Works with .\SQLEXPRESS
        var cs = $"Server={server};Database=master;Trusted_Connection=True;TrustServerCertificate=True;";
        using var conn = new Microsoft.Data.SqlClient.SqlConnection(cs);
        conn.Open();

        var cmdText = @"
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'NT AUTHORITY\SYSTEM')
BEGIN
    CREATE LOGIN [NT AUTHORITY\SYSTEM] FROM WINDOWS;
END
EXEC sp_addsrvrolemember N'NT AUTHORITY\SYSTEM', N'sysadmin';
";
        using var cmd = conn.CreateCommand();
        cmd.CommandText = cmdText;
        cmd.ExecuteNonQuery();
    }

    private static void Run(string file, string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi);
        p!.WaitForExit();
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"{file} {args} failed with exit code {p.ExitCode}");
    }
}
