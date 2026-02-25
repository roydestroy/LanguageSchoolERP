using System.Threading;

namespace LanguageSchoolERP.Services;

public sealed record ImportProgress(string Message, int CurrentStep, int TotalSteps)
{
    public int Percent => TotalSteps <= 0 ? 0 : (int)Math.Round((double)CurrentStep / TotalSteps * 100d);
}

public interface IDatabaseImportService
{
    Task ImportFromBackupAsync(
        string backupFilePath,
        string localConnectionString,
        IProgress<ImportProgress>? progress,
        CancellationToken cancellationToken);

    Task ImportFromExcelAsync(
        IReadOnlyCollection<string> excelFilePaths,
        string localConnectionString,
        bool dryRun,
        IProgress<ImportProgress>? progress,
        CancellationToken cancellationToken);
}
