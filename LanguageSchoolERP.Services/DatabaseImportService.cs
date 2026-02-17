using LanguageSchoolERP.Core.Models;
using LanguageSchoolERP.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LanguageSchoolERP.Services;

public sealed class DatabaseImportService : IDatabaseImportService
{
    public async Task ImportFromRemoteAsync(
        string remoteConnectionString,
        string localConnectionString,
        bool wipeLocalFirst,
        IProgress<ImportProgress>? progress,
        CancellationToken cancellationToken)
    {
        await using var remoteDb = CreateDbContext(remoteConnectionString);
        await using var localDb = CreateDbContext(localConnectionString);

        progress?.Report(new ImportProgress("Ensuring local database exists and migrations are applied...", 0, 1));
        await localDb.Database.MigrateAsync(cancellationToken);

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

        var totalSteps = (wipeLocalFirst ? 1 : 0) + importActions.Length;
        var currentStep = 0;

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

    private static SchoolDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<SchoolDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new SchoolDbContext(options);
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
            if (entityType is null)
            {
                continue;
            }

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
        if (pk is null || pk.Properties.Count != 1)
        {
            return false;
        }

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
