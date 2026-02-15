namespace LanguageSchoolERP.Services;

public static class ContractPathService
{
    public static string GetContractFolder(string academicYear, string studentLastName, string studentFirstName)
    {
        var safeStudent = SanitizeFileName($"{studentLastName}_{studentFirstName}".Trim('_'));
        return Path.Combine(AppContext.BaseDirectory, "Contracts", SanitizeFileName(academicYear), safeStudent);
    }

    public static string GetContractDocxPath(string folder, Guid contractId) => Path.Combine(folder, $"{contractId}.docx");

    public static string GetContractPdfPath(string folder, Guid contractId) => Path.Combine(folder, $"{contractId}.pdf");

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(ch => invalid.Contains(ch) ? '_' : ch));
    }
}
