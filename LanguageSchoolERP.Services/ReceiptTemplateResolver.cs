using System;
using System.IO;

namespace LanguageSchoolERP.Services;

public static class ReceiptTemplateResolver
{
    public static string GetTemplatePath(string databaseName)
    {
        var relative = databaseName switch
        {
            "FilotheiSchoolERP" => @"Templates\ΑΠΟΔΕΙΞΗ ΦΙΛΟΘΕΗ.xlsx",
            "NeaIoniaSchoolERP" => @"Templates\ΑΠΟΔΕΙΞΗ Ν.ΙΩΝΙΑ.xlsx",
            _ => @"Templates\ΑΠΟΔΕΙΞΗ ΦΙΛΟΘΕΗ.xlsx"
        };

        // Absolute path where the exe runs:
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(baseDir, relative);
    }
}
