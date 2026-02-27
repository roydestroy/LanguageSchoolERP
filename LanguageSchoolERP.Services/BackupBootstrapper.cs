using System.Diagnostics;

namespace LanguageSchoolERP.Services;

public static class BackupBootstrapper
{
    private const string TaskName = "LanguageSchoolERP Automatic Backup";

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

        Run("schtasks",
            $"/Create /F /SC MINUTE /MO {safeInterval} /TN \"{TaskName}\" /TR \"{tr}\"");

        Run("schtasks", $"/Change /TN \"{TaskName}\" /ENABLE");
    }

    private static void DisableScheduledTaskIfExists()
    {
        var result = RunAllowingNotFound("schtasks", $"/Change /TN \"{TaskName}\" /DISABLE");

        if (result.ExitCode != 0 && !TaskNotFound(result.Output))
            throw new InvalidOperationException($"Failed to disable scheduled task: {result.Output}");
    }

    private static bool TaskNotFound(string output)
    {
        return output.Contains("ERROR: The system cannot find the file specified", StringComparison.OrdinalIgnoreCase)
               || output.Contains("cannot find", StringComparison.OrdinalIgnoreCase)
               || output.Contains("δεν είναι δυνατός ο εντοπισμός", StringComparison.OrdinalIgnoreCase);
    }

    private static void Run(string file, string args)
    {
        var result = RunAllowingNotFound(file, args);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"{file} {args} failed with exit code {result.ExitCode}. {result.Output}");
    }

    private static (int ExitCode, string Output) RunAllowingNotFound(string file, string args)
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
