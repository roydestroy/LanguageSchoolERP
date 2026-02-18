using System.Threading;

namespace LanguageSchoolERP.Services;

public sealed record ImportProgress(string Message, int CurrentStep, int TotalSteps)
{
    public int Percent => TotalSteps <= 0 ? 0 : (int)Math.Round((double)CurrentStep / TotalSteps * 100d);
}

public interface IDatabaseImportService
{
    Task ImportFromRemoteAsync(
        string remoteConnectionString,
        string localConnectionString,
        bool wipeLocalFirst,
        IProgress<ImportProgress>? progress,
        CancellationToken cancellationToken);

    Task ImportFromBackupAsync(
        string backupFilePath,
        string localConnectionString,
        IProgress<ImportProgress>? progress,
        CancellationToken cancellationToken);
}
