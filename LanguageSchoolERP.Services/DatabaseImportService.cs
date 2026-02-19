using LanguageSchoolERP.Core.Models;
using LanguageSchoolERP.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LanguageSchoolERP.Services;

public sealed class DatabaseImportService : IDatabaseImportService
{
    private readonly IExcelImportRouter _excelImportRouter;

    public DatabaseImportService(IExcelImportRouter excelImportRouter)
    {
        _excelImportRouter = excelImportRouter;
    }
    public async Task ImportFromRemoteAsync(
        string remoteConnectionString,
        string localConnectionString,
        bool wipeLocalFirst,
        IProgress<ImportProgress>? progress,
        CancellationToken cancellationToken)
    {
        // ✅ Ensure the target local database exists BEFORE creating the local DbContext
        progress?.Report(new ImportProgress("Ensuring local database exists...", 0, 1));
        await EnsureDatabaseExistsAsync(localConnectionString, cancellationToken);

        await using var remoteDb = CreateDbContext(remoteConnectionString);
        await using var localDb = CreateDbContext(localConnectionString);

        var importActions = new Func<SchoolDbContext, SchoolDbContext, int, int, IProgress<ImportProgress>?, CancellationToken, Task>[]
        {
            static (remote, local, step, total, report, ct) => ImportEntitySetAsync<AcademicPeriod>(remote, local, step, total, report, ct),
            static (remote, local, step, total, report, ct) => ImportEntitySetAsync<StudyProgram>(remote, local, step, total, report, ct),
            static (remote, local, step, total, report, ct) => ImportEntitySetAsync<ContractTemplate>(remote, local, step, total, report, ct),
            static (remote, local, step, total, report, ct) => ImportEntitySetAsync<Student>(remote, local, step, total, report, ct),
            static (remote, local, step, total, report, ct) => ImportEntitySetAsync<Enrollment>(remote, local, step, total, report, ct),
            static (remote, local, step, total, report, ct) => ImportEntitySetAsync<Payment>(remote, local, step, total, report, ct),
            static (remote, local, step, total, report, ct) => ImportEntitySetAsync<Receipt>(remote, local, step, total, report, ct),
            static (remote, local, step, total, report, ct) => ImportEntitySetAsync<Contract>(remote, local, step, total, report, ct),
            static (remote, local, step, total, report, ct) => ImportEntitySetAsync<ReceiptCounter>(remote, local, step, total, report, ct)
        };

        var totalSteps = (wipeLocalFirst ? 1 : 0) + 1 /* migrate */ + importActions.Length;
        var currentStep = 0;

        progress?.Report(new ImportProgress("Applying migrations...", ++currentStep, totalSteps));
        await localDb.Database.MigrateAsync(cancellationToken);

        await using var tx = await localDb.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (wipeLocalFirst)
            {
                progress?.Report(new ImportProgress("Wiping local database...", ++currentStep, totalSteps));
                await ClearLocalAsync(localDb, cancellationToken);
            }

            foreach (var importAction in importActions)
            {
                await importAction(remoteDb, localDb, ++currentStep, totalSteps, progress, cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
            progress?.Report(new ImportProgress("Import completed successfully.", totalSteps, totalSteps));
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }





    public async Task ImportFromExcelAsync(
        IReadOnlyCollection<string> excelFilePaths,
        string localConnectionString,
        IProgress<ImportProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (excelFilePaths is null || excelFilePaths.Count == 0)
            throw new ArgumentException("At least one Excel file path is required.", nameof(excelFilePaths));

        var files = excelFilePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count == 0)
            throw new ArgumentException("At least one valid Excel file path is required.", nameof(excelFilePaths));

        foreach (var file in files)
        {
            var extension = Path.GetExtension(file);
            if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(extension, ".xlsm", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Unsupported Excel file extension: {file}");
            }

            if (!File.Exists(file))
                throw new FileNotFoundException("Excel file not found.", file);
        }

        var fallbackLocalDatabaseName = new SqlConnectionStringBuilder(localConnectionString).InitialCatalog;
        if (string.IsNullOrWhiteSpace(fallbackLocalDatabaseName))
            throw new InvalidOperationException("Local connection string has no database name.");

        var totalSteps = files.Count * 3;
        var step = 0;

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var route = _excelImportRouter.ResolveRoute(file, fallbackLocalDatabaseName);
            var routedConnectionString = ReplaceDatabaseName(localConnectionString, route.LocalDatabaseName);

            progress?.Report(new ImportProgress($"Routing '{Path.GetFileName(file)}' -> DB '{route.LocalDatabaseName}', Program '{route.DefaultProgramName}'.", ++step, totalSteps));

            await EnsureDatabaseExistsAsync(routedConnectionString, cancellationToken);

            await using var localDb = CreateDbContext(routedConnectionString);
            await localDb.Database.MigrateAsync(cancellationToken);

            var program = await localDb.Programs
                .FirstOrDefaultAsync(p => p.Name == route.DefaultProgramName, cancellationToken);

            if (program is null)
            {
                localDb.Programs.Add(new StudyProgram
                {
                    Name = route.DefaultProgramName,
                    HasTransport = false,
                    HasStudyLab = false,
                    HasBooks = false
                });
                await localDb.SaveChangesAsync(cancellationToken);
                progress?.Report(new ImportProgress($"Upserted StudyProgram '{route.DefaultProgramName}' in '{route.LocalDatabaseName}'.", ++step, totalSteps));
            }
            else
            {
                progress?.Report(new ImportProgress($"StudyProgram '{route.DefaultProgramName}' already exists in '{route.LocalDatabaseName}'.", ++step, totalSteps));
            }

            // TODO: Row import implementation should use route.DefaultProgramName to resolve ProgramId.
            progress?.Report(new ImportProgress($"Prepared import for '{Path.GetFileName(file)}'.", ++step, totalSteps));
        }

        progress?.Report(new ImportProgress("Excel import preparation completed successfully.", totalSteps, totalSteps));
    }

    public async Task ImportFromBackupAsync(
        string backupFilePath,
        string localConnectionString,
        IProgress<ImportProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(backupFilePath))
            throw new ArgumentException("Backup file path is required.", nameof(backupFilePath));

        if (!File.Exists(backupFilePath))
            throw new FileNotFoundException("Backup file not found.", backupFilePath);

        var builder = new SqlConnectionStringBuilder(localConnectionString);
        var databaseName = builder.InitialCatalog;

        if (string.IsNullOrWhiteSpace(databaseName))
            throw new InvalidOperationException("Local connection string has no database name.");

        progress?.Report(new ImportProgress("Validating backup source...", 1, 3));

        var backupPathLiteral = backupFilePath.Replace("'", "''", StringComparison.Ordinal);
        var databaseNameLiteral = databaseName.Replace("]", "]]", StringComparison.Ordinal);

        var setSingleUserSql = $"""
USE [master];
IF DB_ID(N'{databaseNameLiteral}') IS NOT NULL
BEGIN
    ALTER DATABASE [{databaseNameLiteral}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
END
""";

        var restoreSql = $"""
USE [master];
RESTORE DATABASE [{databaseNameLiteral}]
FROM DISK = N'{backupPathLiteral}'
WITH REPLACE, RECOVERY;
""";

        var setMultiUserSql = $"""
USE [master];
IF DB_ID(N'{databaseNameLiteral}') IS NOT NULL
BEGIN
    ALTER DATABASE [{databaseNameLiteral}] SET MULTI_USER;
END
""";

        await ExecuteOnMasterAsync(localConnectionString, setSingleUserSql, progress, 2, 3, "Preparing target database for restore...", cancellationToken);

        try
        {
            await ExecuteOnMasterAsync(localConnectionString, restoreSql, progress, 3, 3, "Restoring database from backup...", cancellationToken);
        }
        finally
        {
            await ExecuteOnMasterAsync(localConnectionString, setMultiUserSql, progress, 3, 3, "Finalizing database restore...", CancellationToken.None);
        }

        progress?.Report(new ImportProgress("Backup restore completed successfully.", 3, 3));
    }
    private static SchoolDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<SchoolDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new SchoolDbContext(options);
    }




    private static string ReplaceDatabaseName(string connectionString, string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = databaseName
        };

        return builder.ConnectionString;
    }

    private static async Task ExecuteOnMasterAsync(
        string connectionString,
        string sql,
        IProgress<ImportProgress>? progress,
        int step,
        int totalSteps,
        string message,
        CancellationToken cancellationToken)
    {
        progress?.Report(new ImportProgress(message, step, totalSteps));

        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master"
        };

        await using var conn = new SqlConnection(builder.ConnectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
    private static async Task EnsureDatabaseExistsAsync(string localConnectionString, CancellationToken ct)
    {
        var builder = new SqlConnectionStringBuilder(localConnectionString);
        var databaseName = builder.InitialCatalog;

        if (string.IsNullOrWhiteSpace(databaseName))
            throw new InvalidOperationException("Local connection string has no database name.");

        // Connect to master instead
        builder.InitialCatalog = "master";

        await using var conn = new SqlConnection(builder.ConnectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
IF DB_ID(@db) IS NULL
BEGIN
    DECLARE @sql nvarchar(max) = N'CREATE DATABASE [' + @db + N']';
    EXEC(@sql);
END";
        cmd.Parameters.AddWithValue("@db", databaseName);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task ClearLocalAsync(SchoolDbContext localDb, CancellationToken cancellationToken)
    {
        // child -> parent order
        var entityTypes = new[]
        {
            localDb.Model.FindEntityType(typeof(Receipt)),
            localDb.Model.FindEntityType(typeof(Payment)),
            localDb.Model.FindEntityType(typeof(Contract)),
            localDb.Model.FindEntityType(typeof(Enrollment)),
            localDb.Model.FindEntityType(typeof(ReceiptCounter)),
            localDb.Model.FindEntityType(typeof(Student)),
            localDb.Model.FindEntityType(typeof(ContractTemplate)),
            localDb.Model.FindEntityType(typeof(StudyProgram)),
            localDb.Model.FindEntityType(typeof(AcademicPeriod))
        };

        foreach (var entityType in entityTypes)
        {
            if (entityType is null) continue;

            var qualified = ToQualifiedTableName(entityType);
            await localDb.Database.ExecuteSqlRawAsync($"DELETE FROM {qualified}", cancellationToken);
        }
    }

    private static async Task ImportEntitySetAsync<TEntity>(
        SchoolDbContext remoteDb,
        SchoolDbContext localDb,
        int currentStep,
        int totalSteps,
        IProgress<ImportProgress>? progress,
        CancellationToken cancellationToken) where TEntity : class
    {
        var entityType = localDb.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"Entity metadata not found for {typeof(TEntity).Name}.");

        var qualifiedName = ToQualifiedTableName(entityType);
        progress?.Report(new ImportProgress($"Importing {qualifiedName}...", currentStep, totalSteps));

        var rows = await remoteDb.Set<TEntity>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            progress?.Report(new ImportProgress($"{qualifiedName}: no rows to import.", currentStep, totalSteps));
            return;
        }

        var identityInsert = IsIdentityPrimaryKey(entityType);
        var originalAutoDetectChangesEnabled = localDb.ChangeTracker.AutoDetectChangesEnabled;

        if (identityInsert)
        {
            await localDb.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT {qualifiedName} ON", cancellationToken);
        }

        try
        {
            localDb.ChangeTracker.AutoDetectChangesEnabled = false;
            await localDb.Set<TEntity>().AddRangeAsync(rows, cancellationToken);
            await localDb.SaveChangesAsync(cancellationToken);
            progress?.Report(new ImportProgress($"{qualifiedName}: imported {rows.Count} rows.", currentStep, totalSteps));
        }
        finally
        {
            localDb.ChangeTracker.AutoDetectChangesEnabled = originalAutoDetectChangesEnabled;

            if (identityInsert)
            {
                await localDb.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT {qualifiedName} OFF", cancellationToken);
            }
        }
    }

    private static bool IsIdentityPrimaryKey(IEntityType entityType)
    {
        var pk = entityType.FindPrimaryKey();
        if (pk is null || pk.Properties.Count != 1) return false;

        var property = pk.Properties[0];
        var clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;

        return property.ValueGenerated == ValueGenerated.OnAdd
               && (clrType == typeof(int) || clrType == typeof(long));
    }

    private static string ToQualifiedTableName(IEntityType entityType)
    {
        var tableName = entityType.GetTableName()
            ?? throw new InvalidOperationException($"Table name missing for {entityType.DisplayName()}.");

        var schema = entityType.GetSchema();
        return string.IsNullOrWhiteSpace(schema)
            ? $"[{Escape(tableName)}]"
            : $"[{Escape(schema)}].[{Escape(tableName)}]";
    }

    private static string Escape(string value) => value.Replace("]", "]]", StringComparison.Ordinal);
}
