namespace LanguageSchoolERP.Services;

public sealed record ExcelImportRoute(string LocalDatabaseName, string DefaultProgramName);

public interface IExcelImportRouter
{
    ExcelImportRoute ResolveRoute(string workbookFilePath, string fallbackLocalDatabaseName);
}

public sealed class FilenamePatternExcelImportRouter : IExcelImportRouter
{
    private static readonly (string Pattern, ExcelImportRoute Route)[] Rules =
    [
        ("EUROLAB", new ExcelImportRoute("FilotheiSchoolERP", "ΠΛΗΡΟΦΟΡΙΚΗ")),
        ("ΜΕΛΕΤΗ ΔΗΜΟΤΙΚΟΥ", new ExcelImportRoute("FilotheiSchoolERP", "ΣΧΟΛΙΚΗ ΜΕΛΕΤΗ"))
    ];

    public ExcelImportRoute ResolveRoute(string workbookFilePath, string fallbackLocalDatabaseName)
    {
        var fileName = Path.GetFileNameWithoutExtension(workbookFilePath) ?? string.Empty;

        foreach (var (pattern, route) in Rules)
        {
            if (fileName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return route;
        }

        return new ExcelImportRoute(fallbackLocalDatabaseName, "ΓΕΝΙΚΟ ΠΡΟΓΡΑΜΜΑ");
    }
}
