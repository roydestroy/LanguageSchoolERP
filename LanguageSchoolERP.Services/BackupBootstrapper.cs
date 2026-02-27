using System.Diagnostics;

namespace LanguageSchoolERP.Services;

public static class BackupBootstrapper
{
    private const string UserTaskName = "\\LanguageSchoolERP\\AutomaticBackup";
    private static readonly string[] LegacyTaskNames =
    [
        "LanguageSchoolERP Automatic Backup",
        "LanguageSchoolERP Backup Upload"
    ];

    public static async Task TryBootstrapAsync()
    {
        var provider = new DatabaseAppSettingsProvider();
        var settings = provider.Settings;

        if (!settings.Backup.Enabled)
            return;

        if (settings.Backup.AutomaticScheduledTaskEnabled)
            EnsureScheduledTask(settings.Backup.IntervalMinutes);
        else
            DisableScheduledTaskIfExists();

        await Task.CompletedTask;
    }

    public static Task SetScheduledTaskEnabledAsync(bool enabled, int intervalMinutes)
    {
        if (enabled)
            EnsureScheduledTask(intervalMinutes);
        else
            DisableScheduledTaskIfExists();

        return Task.CompletedTask;
    }

    private static void EnsureScheduledTask(int intervalMinutes)
    {
        var exePath = Process.GetCurrentProcess().MainModule?.FileName
                      ?? throw new InvalidOperationException("Cannot determine exe path.");

        var tr = $"\"{exePath}\" --run-backup";
        var safeInterval = Math.Max(1, intervalMinutes);

        RunOrThrow("schtasks",
            $"/Create /F /SC MINUTE /MO {safeInterval} /TN \"{UserTaskName}\" /TR \"{tr}\"");

        RunOrThrow("schtasks", $"/Change /TN \"{UserTaskName}\" /ENABLE");
    }

    private static void DisableScheduledTaskIfExists()
    {
        // Try disabling the user-owned task (created by this version of the app).
        TryDisableTask(UserTaskName, throwOnUnexpected: true, ignoreAccessDenied: true);

        // Best-effort: also try to disable legacy task names that might exist from older versions.
        // Legacy tasks may be SYSTEM/admin-owned, so access denied should not break normal-user UX.
        foreach (var legacyName in LegacyTaskNames)
            TryDisableTask(legacyName, throwOnUnexpected: false, ignoreAccessDenied: true);
    }

    private static void TryDisableTask(string taskName, bool throwOnUnexpected, bool ignoreAccessDenied)
    {
        var result = Run("schtasks", $"/Change /TN \"{taskName}\" /DISABLE");

        if (result.ExitCode == 0 || TaskNotFound(result.Output))
            return;

        if (ignoreAccessDenied && AccessDenied(result.Output))
            return;

        if (throwOnUnexpected)
            throw new InvalidOperationException($"Failed to disable scheduled task '{taskName}': {result.Output}");
    }

    private static bool TaskNotFound(string output)
    {
        return output.Contains("ERROR: The system cannot find the file specified", StringComparison.OrdinalIgnoreCase)
               || output.Contains("cannot find", StringComparison.OrdinalIgnoreCase)
               || output.Contains("δεν είναι δυνατός ο εντοπισμός", StringComparison.OrdinalIgnoreCase);
    }

    private static bool AccessDenied(string output)
    {
        return output.Contains("Access is denied", StringComparison.OrdinalIgnoreCase)
               || output.Contains("Πρόσβαση", StringComparison.OrdinalIgnoreCase);
    }

    private static void RunOrThrow(string file, string args)
    {
        var result = Run(file, args);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"{file} {args} failed with exit code {result.ExitCode}. {result.Output}");
    }

    private static (int ExitCode, string Output) Run(string file, string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start process: {file}");

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, $"{output} {error}".Trim());
    }
}
