using LanguageSchoolERP.Core.Models;
using LanguageSchoolERP.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LanguageSchoolERP.Services;

public sealed class DatabaseImportService : IDatabaseImportService
{
    private readonly IExcelImportRouter _excelImportRouter;
    private readonly IExcelWorkbookParser _excelWorkbookParser;

    public DatabaseImportService(IExcelImportRouter excelImportRouter, IExcelWorkbookParser excelWorkbookParser)
    {
        _excelImportRouter = excelImportRouter;
        _excelWorkbookParser = excelWorkbookParser;
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
        bool dryRun,
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

        var totalSteps = Math.Max(1, files.Count * 4);
        var step = 0;

        foreach (var file in files)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                progress?.Report(new ImportProgress("Cancellation requested. Stopping Excel import...", step, totalSteps));
                return;
            }

            var route = _excelImportRouter.ResolveRoute(file, fallbackLocalDatabaseName);
            var routedConnectionString = ReplaceDatabaseName(localConnectionString, route.LocalDatabaseName);

            progress?.Report(new ImportProgress($"Routing '{Path.GetFileName(file)}' -> DB '{route.LocalDatabaseName}', Program '{route.DefaultProgramName}', DryRun={dryRun}.", ++step, totalSteps));

            await EnsureDatabaseExistsAsync(routedConnectionString, CancellationToken.None);

            await using var localDb = CreateDbContext(routedConnectionString);
            await localDb.Database.MigrateAsync(CancellationToken.None);

            var parseResult = await _excelWorkbookParser.ParseAsync(file, route.DefaultProgramName, cancellationToken);
            foreach (var parseError in parseResult.Errors)
            {
                progress?.Report(new ImportProgress($"Parse error [{parseError.SheetName}#{parseError.RowNumber}]: {parseError.Message}", step, totalSteps));
            }

            await using var tx = await localDb.Database.BeginTransactionAsync(CancellationToken.None);
            var summary = new ExcelImportSummary { ErrorRows = parseResult.Errors.Count };

            try
            {
                foreach (var row in parseResult.Rows)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        await tx.RollbackAsync(CancellationToken.None);
                        progress?.Report(new ImportProgress($"Cancellation requested while processing '{Path.GetFileName(file)}'.", step, totalSteps));
                        return;
                    }

                    try
                    {
                        var academicPeriod = await ResolveOrCreateAcademicPeriodAsync(localDb, row.AcademicYearLabel, summary, CancellationToken.None);
                        var program = await ResolveOrCreateProgramAsync(localDb, string.IsNullOrWhiteSpace(row.ProgramName) ? route.DefaultProgramName : row.ProgramName, row.HasTransportationColumn, row.HasStudyLabColumn, row.HasBooksColumn, summary, CancellationToken.None);
                        var student = await ResolveOrCreateStudentAsync(localDb, row.StudentFullName, row.StudentPhone, row.FatherPhone, row.MotherPhone, summary, CancellationToken.None);

                        var enrollment = await localDb.Enrollments
                            .Include(e => e.Payments)
                            .Include(e => e.Program)
                            .FirstOrDefaultAsync(e => e.StudentId == student.StudentId
                                && e.AcademicPeriodId == academicPeriod.AcademicPeriodId
                                && e.Program.Name == program.Name, CancellationToken.None);

                        if (enrollment is null)
                        {
                            enrollment = new Enrollment
                            {
                                StudentId = student.StudentId,
                                AcademicPeriodId = academicPeriod.AcademicPeriodId,
                                ProgramId = program.Id,
                                Program = program,
                                LevelOrClass = row.LevelOrClass,
                                AgreementTotal = row.AgreementTotal,
                                DownPayment = row.DownPayment,
                                IncludesTransportation = row.TransportationMonthlyCost > 0m,
                                TransportationMonthlyPrice = row.TransportationMonthlyCost > 0m ? row.TransportationMonthlyCost : null,
                                HasTransportation = row.TransportationMonthlyCost > 0m,
                                TransportationMonthlyFee = row.TransportationMonthlyCost > 0m ? row.TransportationMonthlyCost : 0m,
                                IncludesStudyLab = row.StudyLabMonthlyCost > 0m,
                                StudyLabMonthlyPrice = row.StudyLabMonthlyCost > 0m ? row.StudyLabMonthlyCost : null,
                                HasStudyLab = row.StudyLabMonthlyCost > 0m,
                                StudyLabMonthlyFee = row.StudyLabMonthlyCost > 0m ? row.StudyLabMonthlyCost : 0m,
                                IsStopped = row.IsDiscontinued,
                                Status = row.IsDiscontinued ? "Stopped" : "Active",
                                StoppedOn = row.IsDiscontinued ? DateTime.Today : null,
                                StopReason = row.IsDiscontinued ? "Excel import: ΔΙΑΚΟΠΗ" : string.Empty,
                                InstallmentCount = row.InstallmentCount > 0 ? row.InstallmentCount : 0,
                                InstallmentStartMonth = row.InstallmentCount > 0 ? row.InstallmentStartMonth : null,
                                Comments = $"Excel import ({row.SourceNote}/{row.SheetName}#{row.RowNumber})"
                            };
                            localDb.Enrollments.Add(enrollment);
                            summary.InsertedEnrollments++;
                        }
                        else
                        {
                            if (row.AgreementTotal > 0m)
                                enrollment.AgreementTotal = row.AgreementTotal;

                            if (row.DownPayment > 0m)
                                enrollment.DownPayment = row.DownPayment;

                            if (!string.IsNullOrWhiteSpace(row.LevelOrClass))
                                enrollment.LevelOrClass = row.LevelOrClass;

                            if (row.TransportationMonthlyCost > 0m)
                            {
                                enrollment.IncludesTransportation = true;
                                enrollment.TransportationMonthlyPrice = row.TransportationMonthlyCost;
                                enrollment.HasTransportation = true;
                                enrollment.TransportationMonthlyFee = row.TransportationMonthlyCost;
                            }

                            if (row.StudyLabMonthlyCost > 0m)
                            {
                                enrollment.IncludesStudyLab = true;
                                enrollment.StudyLabMonthlyPrice = row.StudyLabMonthlyCost;
                                enrollment.HasStudyLab = true;
                                enrollment.StudyLabMonthlyFee = row.StudyLabMonthlyCost;
                            }

                            if (row.InstallmentCount > 0)
                            {
                                enrollment.InstallmentCount = row.InstallmentCount;
                                enrollment.InstallmentStartMonth = row.InstallmentStartMonth;
                            }

                            if (row.IsDiscontinued)
                            {
                                enrollment.IsStopped = true;
                                enrollment.Status = "Stopped";
                                enrollment.StoppedOn ??= DateTime.Today;
                                if (string.IsNullOrWhiteSpace(enrollment.StopReason))
                                    enrollment.StopReason = "Excel import: ΔΙΑΚΟΠΗ";
                            }

                            summary.UpdatedEnrollments++;
                        }

                        if (enrollment.AgreementTotal <= 0m && row.AgreementTotal > 0m)
                            enrollment.AgreementTotal = row.AgreementTotal;

                        if (enrollment.DownPayment <= 0m && row.DownPayment > 0m)
                            enrollment.DownPayment = row.DownPayment;

                        if (row.TransportationMonthlyCost > 0m
                            && (enrollment.TransportationMonthlyFee <= 0m || enrollment.TransportationMonthlyPrice is null))
                        {
                            enrollment.IncludesTransportation = true;
                            enrollment.TransportationMonthlyPrice = row.TransportationMonthlyCost;
                            enrollment.HasTransportation = true;
                            enrollment.TransportationMonthlyFee = row.TransportationMonthlyCost;
                        }

                        if (row.StudyLabMonthlyCost > 0m
                            && (enrollment.StudyLabMonthlyFee <= 0m || enrollment.StudyLabMonthlyPrice is null))
                        {
                            enrollment.IncludesStudyLab = true;
                            enrollment.StudyLabMonthlyPrice = row.StudyLabMonthlyCost;
                            enrollment.HasStudyLab = true;
                            enrollment.StudyLabMonthlyFee = row.StudyLabMonthlyCost;
                        }

                        if (row.IsDiscontinued && !enrollment.IsStopped)
                        {
                            enrollment.IsStopped = true;
                            enrollment.Status = "Stopped";
                            enrollment.StoppedOn ??= DateTime.Today;
                            if (string.IsNullOrWhiteSpace(enrollment.StopReason))
                                enrollment.StopReason = "Excel import: ΔΙΑΚΟΠΗ";
                        }

                        var hasMonthlyPayments = row.MonthlyPayments.Count > 0;
                        if (hasMonthlyPayments)
                        {
                            foreach (var monthly in row.MonthlyPayments)
                            {
                                var existsMonthly = enrollment.Payments.Any(p => p.Amount == monthly.Amount && p.PaymentDate.Date == monthly.PaymentDate.Date);
                                if (existsMonthly)
                                {
                                    summary.SkippedRows++;
                                    continue;
                                }

                                localDb.Payments.Add(new Payment
                                {
                                    EnrollmentId = enrollment.EnrollmentId,
                                    Amount = monthly.Amount,
                                    PaymentDate = monthly.PaymentDate,
                                    Method = PaymentMethod.Cash,
                                    Notes = $"Excel import month {monthly.MonthLabel} ({row.SourceNote}/{row.SheetName}#{row.RowNumber})"
                                });
                                summary.InsertedPayments++;
                            }
                        }
                        else if (row.ConfirmedCollectedAmount.HasValue && row.ConfirmedCollectedAmount.Value > 0)
                        {
                            var paymentDate = row.PaymentDate ?? DateTime.Today;
                            var paymentAmount = row.ConfirmedCollectedAmount.Value;

                            // Avoid duplicating down payment (ΠΡΟΚ/ΛΗ) as a separate payment record.
                            if (row.DownPayment > 0m)
                                paymentAmount = Math.Max(0m, paymentAmount - row.DownPayment);

                            if (paymentAmount <= 0m)
                            {
                                summary.SkippedRows++;
                            }
                            else
                            {
                                var existsPayment = enrollment.Payments.Any(p => p.Amount == paymentAmount && p.PaymentDate.Date == paymentDate.Date);
                                if (!existsPayment)
                                {
                                    localDb.Payments.Add(new Payment
                                    {
                                        EnrollmentId = enrollment.EnrollmentId,
                                        Amount = paymentAmount,
                                        PaymentDate = paymentDate,
                                        Method = PaymentMethod.Cash,
                                        Notes = $"Excel import ({row.SourceNote}/{row.SheetName}#{row.RowNumber})"
                                    });
                                    summary.InsertedPayments++;
                                }
                                else
                                {
                                    summary.SkippedRows++;
                                }
                            }
                        }

                        await localDb.SaveChangesAsync(CancellationToken.None);
                    }
                    catch (OperationCanceledException)
                    {
                        await tx.RollbackAsync(CancellationToken.None);
                        progress?.Report(new ImportProgress($"Cancellation requested while processing '{Path.GetFileName(file)}'.", step, totalSteps));
                        return;
                    }
                    catch (Exception ex)
                    {
                        summary.ErrorRows++;
                        progress?.Report(new ImportProgress($"Row error [{row.SheetName}#{row.RowNumber}]: {ex.Message}", step, totalSteps));
                    }
                }

                if (dryRun)
                {
                    await tx.RollbackAsync(CancellationToken.None);
                    progress?.Report(new ImportProgress($"Dry-run rollback for '{Path.GetFileName(file)}'. {summary.ToLogLine()}", ++step, totalSteps));
                }
                else
                {
                    await tx.CommitAsync(CancellationToken.None);
                    progress?.Report(new ImportProgress($"Imported '{Path.GetFileName(file)}'. {summary.ToLogLine()}", ++step, totalSteps));
                }
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }

            progress?.Report(new ImportProgress($"Workbook processed: {Path.GetFileName(file)}", ++step, totalSteps));
            progress?.Report(new ImportProgress($"Workbook route completed: {route.LocalDatabaseName}", ++step, totalSteps));
        }

        progress?.Report(new ImportProgress("Excel import ETL completed.", totalSteps, totalSteps));
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
        var databaseNameSqlLiteral = databaseName.Replace("'", "''", StringComparison.Ordinal);

        var (dataLogicalName, logLogicalName) = await ReadLogicalFileNamesAsync(localConnectionString, backupFilePath, cancellationToken);
        var (defaultDataPath, defaultLogPath) = await ReadDefaultPathsAsync(localConnectionString, cancellationToken);

        var dataFilePath = Path.Combine(defaultDataPath, $"{databaseName}.mdf").Replace("'", "''", StringComparison.Ordinal);
        var logFilePath = Path.Combine(defaultLogPath, $"{databaseName}_log.ldf").Replace("'", "''", StringComparison.Ordinal);
        var dataLogicalSqlLiteral = dataLogicalName.Replace("'", "''", StringComparison.Ordinal);
        var logLogicalSqlLiteral = logLogicalName.Replace("'", "''", StringComparison.Ordinal);

        var dropSql = $"""
USE [master];
IF DB_ID(N'{databaseNameSqlLiteral}') IS NOT NULL
BEGIN
    ALTER DATABASE [{databaseNameLiteral}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{databaseNameLiteral}];
END
""";

        var restoreSql = $"""
USE [master];
RESTORE DATABASE [{databaseNameLiteral}]
FROM DISK = N'{backupPathLiteral}'
WITH MOVE N'{dataLogicalSqlLiteral}' TO N'{dataFilePath}',
     MOVE N'{logLogicalSqlLiteral}' TO N'{logFilePath}',
     RECOVERY;
""";

        await ExecuteOnMasterAsync(localConnectionString, dropSql, progress, 2, 3, "Dropping target database before restore...", cancellationToken);
        await ExecuteOnMasterAsync(localConnectionString, restoreSql, progress, 3, 3, "Restoring database from backup...", cancellationToken);

        progress?.Report(new ImportProgress("Backup restore completed successfully.", 3, 3));
    }

    private static async Task<(string DataLogicalName, string LogLogicalName)> ReadLogicalFileNamesAsync(
        string connectionString,
        string backupFilePath,
        CancellationToken cancellationToken)
    {
        var backupLiteral = backupFilePath.Replace("'", "''", StringComparison.Ordinal);
        const string dataType = "D";
        const string logType = "L";

        var sql = $"RESTORE FILELISTONLY FROM DISK = N'{backupLiteral}';";

        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master"
        };

        await using var conn = new SqlConnection(builder.ConnectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 0 };
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        string? dataLogical = null;
        string? logLogical = null;

        while (await reader.ReadAsync(cancellationToken))
        {
            var logicalName = reader["LogicalName"]?.ToString();
            var type = reader["Type"]?.ToString();

            if (string.IsNullOrWhiteSpace(logicalName) || string.IsNullOrWhiteSpace(type))
                continue;

            if (dataLogical is null && string.Equals(type, dataType, StringComparison.OrdinalIgnoreCase))
                dataLogical = logicalName;
            else if (logLogical is null && string.Equals(type, logType, StringComparison.OrdinalIgnoreCase))
                logLogical = logicalName;
        }

        if (string.IsNullOrWhiteSpace(dataLogical) || string.IsNullOrWhiteSpace(logLogical))
            throw new InvalidOperationException("Could not determine logical file names from backup.");

        return (dataLogical, logLogical);
    }

    private static async Task<(string DefaultDataPath, string DefaultLogPath)> ReadDefaultPathsAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        const string sql = "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000)), CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(4000));";

        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master"
        };

        await using var conn = new SqlConnection(builder.ConnectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 0 };
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Could not read SQL Server default data/log paths.");

        var dataPath = reader.IsDBNull(0) ? null : reader.GetString(0);
        var logPath = reader.IsDBNull(1) ? null : reader.GetString(1);

        if (string.IsNullOrWhiteSpace(dataPath) || string.IsNullOrWhiteSpace(logPath))
            throw new InvalidOperationException("SQL Server default data/log paths are not available.");

        return (dataPath, logPath);
    }
    private static SchoolDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<SchoolDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new SchoolDbContext(options);
    }




    private static async Task<AcademicPeriod> ResolveOrCreateAcademicPeriodAsync(
        SchoolDbContext db,
        string periodName,
        ExcelImportSummary summary,
        CancellationToken ct)
    {
        var normalized = periodName.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException("Academic period is required.");

        var period = db.AcademicPeriods.Local.FirstOrDefault(p => string.Equals(p.Name, normalized, StringComparison.OrdinalIgnoreCase));
        period ??= await db.AcademicPeriods.FirstOrDefaultAsync(p => p.Name == normalized, ct);
        if (period is not null)
            return period;

        period = new AcademicPeriod { Name = normalized, IsCurrent = false };
        db.AcademicPeriods.Add(period);
        summary.InsertedAcademicPeriods++;
        return period;
    }

    private static async Task<StudyProgram> ResolveOrCreateProgramAsync(
        SchoolDbContext db,
        string programName,
        bool hasTransportationColumn,
        bool hasStudyLabColumn,
        bool hasBooksColumn,
        ExcelImportSummary summary,
        CancellationToken ct)
    {
        var normalized = programName.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException("Program name is required.");

        var program = db.Programs.Local.FirstOrDefault(p => string.Equals(p.Name, normalized, StringComparison.OrdinalIgnoreCase));
        program ??= await db.Programs.FirstOrDefaultAsync(p => p.Name == normalized, ct);
        if (program is not null)
        {
            if (hasTransportationColumn && !program.HasTransport)
                program.HasTransport = true;

            if (hasStudyLabColumn && !program.HasStudyLab)
                program.HasStudyLab = true;

            if (hasBooksColumn && !program.HasBooks)
                program.HasBooks = true;

            return program;
        }

        program = new StudyProgram
        {
            Name = normalized,
            HasTransport = hasTransportationColumn,
            HasStudyLab = hasStudyLabColumn,
            HasBooks = hasBooksColumn
        };
        db.Programs.Add(program);
        summary.InsertedPrograms++;
        return program;
    }

    private static async Task<Student> ResolveOrCreateStudentAsync(
        SchoolDbContext db,
        string fullName,
        string? normalizedStudentPhone,
        string? normalizedFatherPhone,
        string? normalizedMotherPhone,
        ExcelImportSummary summary,
        CancellationToken ct)
    {
        var normalizedName = fullName.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
            throw new InvalidOperationException("Student full name is required.");

        var (normalizedFirstName, normalizedLastName) = SplitFullName(normalizedName);
        var (normalizedStudentMobile, normalizedStudentLandline) = SplitPhoneByPrefix(normalizedStudentPhone);
        var (normalizedFatherMobile, normalizedFatherLandline) = SplitPhoneByPrefix(normalizedFatherPhone);
        var (normalizedMotherMobile, normalizedMotherLandline) = SplitPhoneByPrefix(normalizedMotherPhone);

        Student? student = null;

        if (!string.IsNullOrWhiteSpace(normalizedStudentMobile))
        {
            student = db.Students.Local.FirstOrDefault(s => s.FirstName == normalizedFirstName && s.LastName == normalizedLastName && s.Mobile == normalizedStudentMobile)
                ?? await db.Students.FirstOrDefaultAsync(s => s.FirstName == normalizedFirstName && s.LastName == normalizedLastName && s.Mobile == normalizedStudentMobile, ct);
        }

        student ??= db.Students.Local.FirstOrDefault(s => s.FirstName == normalizedFirstName && s.LastName == normalizedLastName)
            ?? await db.Students.FirstOrDefaultAsync(s => s.FirstName == normalizedFirstName && s.LastName == normalizedLastName, ct);

        if (student is null && !string.IsNullOrWhiteSpace(normalizedStudentMobile))
        {
            student = db.Students.Local.FirstOrDefault(s => s.Mobile == normalizedStudentMobile)
                ?? await db.Students.FirstOrDefaultAsync(s => s.Mobile == normalizedStudentMobile, ct);
        }

        if (student is not null)
        {
            student.NormalizedFirstName = student.FirstName.ToUpperInvariant();
            student.NormalizedLastName = student.LastName.ToUpperInvariant();

            if (!string.IsNullOrWhiteSpace(normalizedStudentMobile))
                student.Mobile = normalizedStudentMobile;

            if (!string.IsNullOrWhiteSpace(normalizedStudentLandline))
                student.Landline = normalizedStudentLandline;

            if (!string.IsNullOrWhiteSpace(normalizedFatherMobile))
                student.FatherMobile = normalizedFatherMobile;

            if (!string.IsNullOrWhiteSpace(normalizedFatherLandline))
                student.FatherLandline = normalizedFatherLandline;

            if (!string.IsNullOrWhiteSpace(normalizedMotherMobile))
                student.MotherMobile = normalizedMotherMobile;

            if (!string.IsNullOrWhiteSpace(normalizedMotherLandline))
                student.MotherLandline = normalizedMotherLandline;

            return student;
        }

        student = new Student
        {
            FirstName = normalizedFirstName,
            LastName = normalizedLastName,
            NormalizedFirstName = normalizedFirstName.ToUpperInvariant(),
            NormalizedLastName = normalizedLastName.ToUpperInvariant(),
            Mobile = normalizedStudentMobile ?? string.Empty,
            Landline = normalizedStudentLandline ?? string.Empty,
            FatherMobile = normalizedFatherMobile ?? string.Empty,
            FatherLandline = normalizedFatherLandline ?? string.Empty,
            MotherMobile = normalizedMotherMobile ?? string.Empty,
            MotherLandline = normalizedMotherLandline ?? string.Empty
        };
        db.Students.Add(student);
        summary.InsertedStudents++;
        return student;
    }

    private static (string FirstName, string LastName) SplitFullName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return (string.Empty, string.Empty);

        if (parts.Length == 1)
            return (parts[0], string.Empty);

        return (string.Join(' ', parts.Take(parts.Length - 1)), parts[^1]);
    }

    private static (string? Mobile, string? Landline) SplitPhoneByPrefix(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return (null, null);

        var value = phone.Trim();
        if (value.StartsWith('2'))
            return (null, value);

        if (value.StartsWith('6'))
            return (value, null);

        return (value, null);
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
