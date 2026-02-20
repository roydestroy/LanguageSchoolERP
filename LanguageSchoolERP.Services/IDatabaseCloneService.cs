namespace LanguageSchoolERP.Services;

public interface IDatabaseCloneService
{
    Task CloneFromLatestBackupAsync(School school, IProgress<string> progress, CancellationToken ct);
}
