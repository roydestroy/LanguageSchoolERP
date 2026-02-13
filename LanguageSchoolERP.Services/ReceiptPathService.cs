using System.IO;

namespace LanguageSchoolERP.Services;

public static class ReceiptPathService
{

    public static string GetStudentFolder(string baseDir, string dbName, string academicYear, string studentFullName)
    {
        var safeName = MakeSafeFileName(studentFullName);
        return Path.Combine(baseDir, dbName, academicYear, safeName);
    }

    public static string GetReceiptPdfPath(string studentFolder, int receiptNumber)
    {
        return Path.Combine(studentFolder, $"Receipt_{receiptNumber}.pdf");
    }

    private static string MakeSafeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim();
    }
}
