namespace LanguageSchoolERP.Services;

public static class ContractGenerationLog
{
    private static readonly object Sync = new();

    public static string LogFilePath
    {
        get
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LanguageSchoolERP",
                "Logs");
            Directory.CreateDirectory(root);
            return Path.Combine(root, "contract-generation.log");
        }
    }

    public static void Write(string message, Exception? ex = null)
    {
        try
        {
            var lines = new List<string>
            {
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}"
            };

            if (ex is not null)
                lines.Add(ex.ToString());

            lock (Sync)
            {
                File.AppendAllLines(LogFilePath, lines);
            }
        }
        catch
        {
            // ignore logging failures
        }
    }
}
