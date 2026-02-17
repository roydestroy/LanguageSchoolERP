namespace LanguageSchoolERP.Services;

public static class UpdaterLog
{
    private static readonly object Sync = new();

    public static void Write(string source, string message, Exception? ex = null)
    {
        try
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LanguageSchoolERP",
                "Logs");
            Directory.CreateDirectory(root);

            var file = Path.Combine(root, "updater.log");
            var lines = new List<string>
            {
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] {message}"
            };

            if (ex is not null)
            {
                lines.Add(ex.ToString());
            }

            lock (Sync)
            {
                File.AppendAllLines(file, lines);
            }
        }
        catch
        {
            // ignore logging failures
        }
    }
}
