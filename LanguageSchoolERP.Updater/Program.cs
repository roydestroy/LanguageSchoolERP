using System.Diagnostics;
using System.IO.Compression;

var argsMap = ParseArgs(args);
if (!argsMap.TryGetValue("pid", out var pidRaw) ||
    !int.TryParse(pidRaw, out var pid) ||
    !argsMap.TryGetValue("zip", out var zipPath) ||
    !argsMap.TryGetValue("installDir", out var installDir) ||
    !argsMap.TryGetValue("exe", out var exeArg))
{
    Log("Invalid arguments. Expected --pid --zip --installDir --exe.");
    return 1;
}

try
{
    Log($"Updater started. pid={pid}, zip='{zipPath}', installDir='{installDir}', exe='{exeArg}'.");

    WaitForProcessExit(pid);

    if (!File.Exists(zipPath))
        throw new FileNotFoundException($"Update zip not found: {zipPath}");

    var tempRoot = Path.Combine(Path.GetTempPath(), "LanguageSchoolERP", "Updater", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    var extractDir = Path.Combine(tempRoot, "extract");
    Directory.CreateDirectory(extractDir);

    Log($"Extracting zip to '{extractDir}'.");
    ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);

    var payloadRoot = FindPayloadRoot(extractDir);
    Log($"Copying files from '{payloadRoot}' to '{installDir}'.");

    CopyDirectory(payloadRoot, installDir);

    // exe can be full path or just filename.
    var mainExe = Path.IsPathRooted(exeArg) ? exeArg : Path.Combine(installDir, exeArg);
    if (!File.Exists(mainExe))
        throw new FileNotFoundException($"Main executable not found after update: {mainExe}");

    Process.Start(new ProcessStartInfo
    {
        FileName = mainExe,
        WorkingDirectory = Path.GetDirectoryName(mainExe) ?? installDir,
        UseShellExecute = true
    });

    // Cleanup extracted temp files
    try
    {
        if (Directory.Exists(tempRoot))
            Directory.Delete(tempRoot, true);
    }
    catch
    {
        // ignore cleanup failures
    }

    Log("Update completed successfully.");
    return 0;

}
catch (Exception ex)
{
    Log("Update failed.", ex);
    return 1;
}

static Dictionary<string, string> ParseArgs(string[] args)
{
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length; i++)
    {
        var key = args[i];
        if (!key.StartsWith("--", StringComparison.Ordinal))
            continue;

        if (i + 1 >= args.Length)
            break;

        map[key[2..]] = args[i + 1];
        i++;
    }

    return map;
}

static void WaitForProcessExit(int pid)
{
    try
    {
        if (pid <= 0)
        {
            Log($"pid={pid} so no wait.");
            return;
        }

        var process = Process.GetProcessById(pid);
        Log($"Waiting for process {pid} to exit.");
        process.WaitForExit(120000);

        if (!process.HasExited)
        {
            Log($"Process {pid} did not exit in time. Waiting without timeout.");
            process.WaitForExit();
        }
    }
    catch (ArgumentException)
    {
        Log($"Process {pid} already exited.");
    }
}

static string FindPayloadRoot(string extractDir)
{
    var files = Directory.GetFiles(extractDir);
    if (files.Length > 0)
        return extractDir;

    var dirs = Directory.GetDirectories(extractDir);
    if (dirs.Length == 1)
        return dirs[0];

    return extractDir;
}

static void CopyDirectory(string sourceDir, string destinationDir)
{
    Directory.CreateDirectory(destinationDir);

    foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
    {
        var relative = Path.GetRelativePath(sourceDir, directory);
        Directory.CreateDirectory(Path.Combine(destinationDir, relative));
    }

    foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
    {
        var relative = Path.GetRelativePath(sourceDir, file);
        var target = Path.Combine(destinationDir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        var fileName = Path.GetFileName(file);

        // Critical: do NOT overwrite the updater binaries while updater is running.
        if (IsUpdaterSelfFile(fileName))
        {
            Log($"Skipping updater self-file '{fileName}'.");
            continue;
        }

        CopyFileWithRetry(file, target, overwrite: true, attempts: 10, delayMs: 250);
    }
}

static bool IsUpdaterSelfFile(string fileName)
{
    // Adjust these if you rename the updater project/exe.
    return fileName.Equals("LanguageSchoolERP.Updater.exe", StringComparison.OrdinalIgnoreCase)
        || fileName.Equals("LanguageSchoolERP.Updater.dll", StringComparison.OrdinalIgnoreCase)
        || fileName.Equals("LanguageSchoolERP.Updater.deps.json", StringComparison.OrdinalIgnoreCase)
        || fileName.Equals("LanguageSchoolERP.Updater.runtimeconfig.json", StringComparison.OrdinalIgnoreCase);
}

static void CopyFileWithRetry(string source, string target, bool overwrite, int attempts, int delayMs)
{
    // Ensure target isn't read-only (can happen with some deployment scenarios)
    if (File.Exists(target))
    {
        try
        {
            var attrs = File.GetAttributes(target);
            if ((attrs & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(target, attrs & ~FileAttributes.ReadOnly);
        }
        catch
        {
            // ignore
        }
    }

    for (var attempt = 1; attempt <= attempts; attempt++)
    {
        try
        {
            File.Copy(source, target, overwrite);
            return;
        }
        catch (IOException ex) when (attempt < attempts)
        {
            Log($"File copy locked. attempt={attempt}/{attempts} source='{source}' target='{target}'. {ex.Message}");
            Thread.Sleep(delayMs);
        }
        catch (UnauthorizedAccessException ex) when (attempt < attempts)
        {
            Log($"File copy unauthorized. attempt={attempt}/{attempts} source='{source}' target='{target}'. {ex.Message}");
            Thread.Sleep(delayMs);
        }
    }

    // last attempt throws if it fails
    File.Copy(source, target, overwrite);
}

static void Log(string message, Exception? ex = null)
{
    try
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LanguageSchoolERP",
            "Logs");
        Directory.CreateDirectory(logDir);
        var logFile = Path.Combine(logDir, "updater.log");

        var lines = new List<string> { $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Updater] {message}" };
        if (ex is not null)
            lines.Add(ex.ToString());

        File.AppendAllLines(logFile, lines);
    }
    catch
    {
        // ignore log failures
    }
}
