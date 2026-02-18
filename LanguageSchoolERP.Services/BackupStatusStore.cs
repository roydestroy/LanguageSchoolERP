using System;
using System.IO;
using System.Text.Json;

namespace LanguageSchoolERP.Services;

public sealed class BackupStatusSnapshot
{
    public DateTime? LastAttemptUtc { get; set; }
    public DateTime? LastSuccessUtc { get; set; }
    public string? LastResult { get; set; }
    public string? LastError { get; set; }
}

public static class BackupStatusStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly string StatusDirectory = @"C:\ProgramData\LanguageSchoolERP";
    private static readonly string StatusPath = Path.Combine(StatusDirectory, "backup-status.json");

    public static BackupStatusSnapshot? TryRead()
    {
        try
        {
            if (!File.Exists(StatusPath))
                return null;

            var json = File.ReadAllText(StatusPath);
            return JsonSerializer.Deserialize<BackupStatusSnapshot>(json);
        }
        catch
        {
            return null;
        }
    }

    public static void TryWriteAttempt(DateTime attemptUtc)
    {
        TryUpdate(snapshot =>
        {
            snapshot.LastAttemptUtc = attemptUtc;
        });
    }

    public static void TryWriteSuccess(DateTime successUtc)
    {
        TryUpdate(snapshot =>
        {
            snapshot.LastSuccessUtc = successUtc;
            snapshot.LastResult = "Success";
            snapshot.LastError = null;
        });
    }

    public static void TryWriteFailure(string? error)
    {
        TryUpdate(snapshot =>
        {
            snapshot.LastResult = "Failed";
            snapshot.LastError = Truncate(error, 500);
        });
    }

    private static void TryUpdate(Action<BackupStatusSnapshot> apply)
    {
        try
        {
            Directory.CreateDirectory(StatusDirectory);

            var snapshot = TryRead() ?? new BackupStatusSnapshot();
            apply(snapshot);

            var json = JsonSerializer.Serialize(snapshot, JsonOptions);
            File.WriteAllText(StatusPath, json);
        }
        catch
        {
            // Must never crash backup flow because of backup-status persistence.
        }
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }
}
