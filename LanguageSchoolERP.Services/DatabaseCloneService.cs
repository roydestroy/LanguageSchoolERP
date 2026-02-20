using Microsoft.Data.SqlClient;

namespace LanguageSchoolERP.Services;

public sealed class DatabaseCloneService : IDatabaseCloneService
{
    private readonly DatabaseAppSettingsProvider _settingsProvider;

    public DatabaseCloneService(DatabaseAppSettingsProvider settingsProvider)
    {
        _settingsProvider = settingsProvider;
    }

    public async Task CloneFromLatestBackupAsync(School school, IProgress<string> progress, CancellationToken ct)
    {
        var settings = _settingsProvider.Settings;
        var (databaseName, branchFolder, latestBackupFileName) = ResolveSchoolSettings(school);
        var localMasterConnection = BuildMasterConnectionString(settings.Local.Server);

        var backupPath = await CopyBackupLocallyAsync(settings, branchFolder, latestBackupFileName, progress, ct);

        try
        {
            await using var connection = new SqlConnection(localMasterConnection);
            await connection.OpenAsync(ct);

            progress.Report("Dropping existing local DB...");
            await DropDatabaseIfExistsAsync(connection, databaseName, ct);

            progress.Report("Reading logical file names...");
            var (dataLogicalName, logLogicalName) = await ReadLogicalFileNamesAsync(connection, backupPath, ct);

            var (defaultDataPath, defaultLogPath) = await ReadDefaultPathsAsync(connection, ct);
            var dataFilePath = Path.Combine(defaultDataPath, $"{databaseName}.mdf");
            var logFilePath = Path.Combine(defaultLogPath, $"{databaseName}_log.ldf");

            progress.Report("Restoring database...");
            await RestoreDatabaseAsync(
                connection,
                databaseName,
                backupPath,
                dataLogicalName,
                logLogicalName,
                dataFilePath,
                logFilePath,
                ct);

            progress.Report("Restore complete.");
        }
        finally
        {
            TryDeleteTempBackup(backupPath);
        }
    }

    private static (string DatabaseName, string BranchFolder, string LatestBackupFileName) ResolveSchoolSettings(School school)
    {
        return school switch
        {
            School.Filothei => ("FilotheiSchoolERP", "Filothei", "FilotheiSchoolERP_latest.bak"),
            School.NeaIonia => ("NeaIoniaSchoolERP", "NeaIonia", "NeaIoniaSchoolERP_latest.bak"),
            _ => throw new ArgumentOutOfRangeException(nameof(school), school, "Unsupported school for clone operation.")
        };
    }

    private async Task<string> CopyBackupLocallyAsync(
        DatabaseAppSettings settings,
        string branchFolder,
        string latestBackupFileName,
        IProgress<string> progress,
        CancellationToken ct)
    {
        progress.Report("Copying backup locally...");

        var remoteRoot = settings.Clone.BackupUncRoot;
        if (string.IsNullOrWhiteSpace(remoteRoot))
            throw new InvalidOperationException("Cannot access backup path: UNC backup root is not configured.");

        var sourceBackupPath = Path.Combine(remoteRoot, branchFolder, latestBackupFileName);
        var tempDirectory = settings.Clone.LocalTempDirectory;
        Directory.CreateDirectory(tempDirectory);

        var localBackupPath = Path.Combine(tempDirectory, $"{Guid.NewGuid():N}_{latestBackupFileName}");

        using var networkConnection = new NetworkConnection(
            remoteRoot,
            settings.Backup.RemoteShareUser,
            settings.Backup.RemoteSharePassword);

        if (!File.Exists(sourceBackupPath))
            throw new FileNotFoundException($"Cannot access backup path '{sourceBackupPath}'.", sourceBackupPath);

        await using var sourceStream = new FileStream(sourceBackupPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, useAsync: true);
        await using var destinationStream = new FileStream(localBackupPath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, useAsync: true);
        await sourceStream.CopyToAsync(destinationStream, ct);

        return localBackupPath;
    }

    private static async Task DropDatabaseIfExistsAsync(SqlConnection connection, string databaseName, CancellationToken ct)
    {
        var nameLiteral = databaseName.Replace("]", "]]", StringComparison.Ordinal);

        var sql = $"""
IF DB_ID(N'{databaseName.Replace("'", "''", StringComparison.Ordinal)}') IS NOT NULL
BEGIN
    ALTER DATABASE [{nameLiteral}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{nameLiteral}];
END
""";

        await ExecuteNonQueryAsync(connection, sql, ct);
    }

    private static async Task<(string DataLogicalName, string LogLogicalName)> ReadLogicalFileNamesAsync(SqlConnection connection, string backupPath, CancellationToken ct)
    {
        var backupLiteral = backupPath.Replace("'", "''", StringComparison.Ordinal);

        var sql = $"RESTORE FILELISTONLY FROM DISK = N'{backupLiteral}';";
        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = 0 };
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        string? dataLogicalName = null;
        string? logLogicalName = null;

        while (await reader.ReadAsync(ct))
        {
            var type = reader["Type"]?.ToString();
            var logicalName = reader["LogicalName"]?.ToString();
            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(logicalName))
                continue;

            if (string.Equals(type, "D", StringComparison.OrdinalIgnoreCase) && dataLogicalName is null)
                dataLogicalName = logicalName;

            if (string.Equals(type, "L", StringComparison.OrdinalIgnoreCase) && logLogicalName is null)
                logLogicalName = logicalName;
        }

        if (string.IsNullOrWhiteSpace(dataLogicalName) || string.IsNullOrWhiteSpace(logLogicalName))
            throw new InvalidOperationException("Unable to resolve logical data/log file names from backup.");

        return (dataLogicalName, logLogicalName);
    }

    private static async Task<(string DataPath, string LogPath)> ReadDefaultPathsAsync(SqlConnection connection, CancellationToken ct)
    {
        const string sql = """
SELECT
    CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultDataPath')) AS DataPath,
    CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultLogPath')) AS LogPath;
""";

        await using var cmd = new SqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new InvalidOperationException("Could not resolve SQL Server default data path.");

        var dataPath = reader["DataPath"]?.ToString();
        var logPath = reader["LogPath"]?.ToString();

        if (string.IsNullOrWhiteSpace(dataPath))
            throw new InvalidOperationException("SQL Server InstanceDefaultDataPath is not available.");

        dataPath = EnsureDirectorySeparator(dataPath);
        logPath = string.IsNullOrWhiteSpace(logPath) ? dataPath : EnsureDirectorySeparator(logPath);

        return (dataPath, logPath);
    }

    private static async Task RestoreDatabaseAsync(
        SqlConnection connection,
        string databaseName,
        string backupPath,
        string dataLogicalName,
        string logLogicalName,
        string dataFilePath,
        string logFilePath,
        CancellationToken ct)
    {
        var dbLiteral = databaseName.Replace("]", "]]", StringComparison.Ordinal);
        var backupLiteral = backupPath.Replace("'", "''", StringComparison.Ordinal);
        var dataLogicalLiteral = dataLogicalName.Replace("'", "''", StringComparison.Ordinal);
        var logLogicalLiteral = logLogicalName.Replace("'", "''", StringComparison.Ordinal);
        var dataFileLiteral = dataFilePath.Replace("'", "''", StringComparison.Ordinal);
        var logFileLiteral = logFilePath.Replace("'", "''", StringComparison.Ordinal);

        var sql = $"""
RESTORE DATABASE [{dbLiteral}]
FROM DISK = N'{backupLiteral}'
WITH MOVE N'{dataLogicalLiteral}' TO N'{dataFileLiteral}',
     MOVE N'{logLogicalLiteral}' TO N'{logFileLiteral}',
     REPLACE,
     RECOVERY;
""";

        await ExecuteNonQueryAsync(connection, sql, ct);
    }

    private static async Task ExecuteNonQueryAsync(SqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 0 };
        await command.ExecuteNonQueryAsync(ct);
    }

    private static string BuildMasterConnectionString(string server)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = string.IsNullOrWhiteSpace(server) ? @".\SQLEXPRESS" : server,
            InitialCatalog = "master",
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            Encrypt = true
        };

        return builder.ConnectionString;
    }

    private static string EnsureDirectorySeparator(string value)
    {
        return value.EndsWith(Path.DirectorySeparatorChar) || value.EndsWith(Path.AltDirectorySeparatorChar)
            ? value
            : value + Path.DirectorySeparatorChar;
    }

    private static void TryDeleteTempBackup(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best effort cleanup
        }
    }
}
