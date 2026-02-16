namespace LanguageSchoolERP.Services;

public static class ContractPathService
{
    public static string GetContractFolder(string academicYear, string studentLastName, string studentFirstName)
    {
        var safeStudent = SanitizeFileName($"{studentLastName}_{studentFirstName}".Trim('_'));
        return Path.Combine(AppContext.BaseDirectory, "Contracts", SanitizeFileName(academicYear), safeStudent);
    }

    public static string GetContractDocxPath(string folder, string programTitle, Guid contractId)
    {
        var baseName = BuildBaseFileName(programTitle, contractId);
        return Path.Combine(folder, $"{baseName}.docx");
    }

    public static string GetContractPdfPath(string folder, string programTitle, Guid contractId)
    {
        var baseName = BuildBaseFileName(programTitle, contractId);
        return Path.Combine(folder, $"{baseName}.pdf");
    }

    public static string GetContractPdfPathFromDocxPath(string docxPath)
    {
        var folder = Path.GetDirectoryName(docxPath) ?? AppContext.BaseDirectory;
        var fileName = Path.GetFileNameWithoutExtension(docxPath);
        return Path.Combine(folder, $"{fileName}.pdf");
    }

    private static string BuildBaseFileName(string programTitle, Guid contractId)
    {
        var safeProgram = SanitizeFileName(programTitle);
        if (string.IsNullOrWhiteSpace(safeProgram))
            safeProgram = "CONTRACT";

        return $"{safeProgram}_{contractId:N}";
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat((value ?? string.Empty).Select(ch => invalid.Contains(ch) ? '_' : ch));
    }
}
