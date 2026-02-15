using System.Diagnostics;
using Microsoft.Office.Interop.Word;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using Application = Microsoft.Office.Interop.Word.Application;

namespace LanguageSchoolERP.Services;

public sealed class ContractDocumentService
{
    private static readonly HashSet<string> BoldBookmarks = new(StringComparer.OrdinalIgnoreCase)
    {
        "on_up", "on_sp", "per_prg", "tit_prg", "sun_pos", "prok_pos", "cur"
    };

    public string GenerateDocxFromTemplate(
        string templatePath,
        string outputDocxPath,
        Dictionary<string, string> bookmarkValues,
        int installmentCount,
        bool financedPositive)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputDocxPath)!);

        Application? app = null;
        Document? doc = null;
        object missing = Type.Missing;

        try
        {
            app = new Application { Visible = false, DisplayAlerts = WdAlertLevel.wdAlertsNone };
            object readOnly = false;
            object isVisible = false;
            object templateObj = templatePath;
            doc = app.Documents.Open(ref templateObj, ref missing, ref readOnly, ref missing, ref missing, ref missing,
                ref missing, ref missing, ref missing, ref missing, ref missing, ref isVisible, ref missing, ref missing,
                ref missing, ref missing);

            foreach (var (bookmark, value) in bookmarkValues)
            {
                if (!doc.Bookmarks.Exists(bookmark))
                    continue;

                var range = doc.Bookmarks[bookmark].Range;
                var valueToWrite = value;

                if (string.Equals(bookmark, "slab", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(value))
                {
                    valueToWrite = "\r" + value.TrimStart('\r', '\n');
                }

                range.Text = valueToWrite;
                if (BoldBookmarks.Contains(bookmark))
                    range.Bold = 1;

                object bookmarkName = bookmark;
                doc.Bookmarks.Add(bookmarkName.ToString(), range);
            }

            if (installmentCount <= 0 || !financedPositive)
                RemoveInstallmentsTable(doc);
            else
                RemoveUnusedInstallmentRows(doc, installmentCount);

            object outputPath = outputDocxPath;
            doc.SaveAs2(ref outputPath, ref missing, ref missing, ref missing, ref missing, ref missing, ref missing,
                ref missing, ref missing, ref missing, ref missing, ref missing, ref missing, ref missing, ref missing,
                ref missing, ref missing);

            return outputDocxPath;
        }
        finally
        {
            if (doc is not null)
            {
                object saveChanges = WdSaveOptions.wdDoNotSaveChanges;
                doc.Close(ref saveChanges, ref missing, ref missing);
            }

            if (app is not null)
                app.Quit(ref missing, ref missing, ref missing);
        }
    }

    public string ExportPdfWithPageDuplication(string docxPath, string pdfPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(pdfPath)!);
        var tempPdf = Path.Combine(Path.GetTempPath(), $"contract_{Guid.NewGuid():N}.pdf");

        try
        {
            ExportWordToPdf(docxPath, tempPdf);

            using var input = PdfReader.Open(tempPdf, PdfDocumentOpenMode.Import);
            if (input.PageCount < 2)
            {
                File.Copy(tempPdf, pdfPath, overwrite: true);
                return pdfPath;
            }

            using var output = new PdfDocument();
            output.AddPage(input.Pages[0]);
            output.AddPage(input.Pages[1]);
            output.AddPage(input.Pages[0]);
            output.AddPage(input.Pages[1]);

            for (var i = 2; i < input.PageCount; i++)
                output.AddPage(input.Pages[i]);

            output.Save(pdfPath);
            return pdfPath;
        }
        finally
        {
            if (File.Exists(tempPdf))
                File.Delete(tempPdf);
        }
    }

    private static void ExportWordToPdf(string docxPath, string pdfPath)
    {
        Application? app = null;
        Document? doc = null;
        object missing = Type.Missing;

        try
        {
            app = new Application { Visible = false, DisplayAlerts = WdAlertLevel.wdAlertsNone };
            object fileName = docxPath;
            object readOnly = true;
            object isVisible = false;
            doc = app.Documents.Open(ref fileName, ref missing, ref readOnly, ref missing, ref missing, ref missing,
                ref missing, ref missing, ref missing, ref missing, ref missing, ref isVisible, ref missing, ref missing,
                ref missing, ref missing);

            doc.ExportAsFixedFormat(pdfPath, WdExportFormat.wdExportFormatPDF);
        }
        finally
        {
            if (doc is not null)
            {
                object saveChanges = WdSaveOptions.wdDoNotSaveChanges;
                doc.Close(ref saveChanges, ref missing, ref missing);
            }

            if (app is not null)
                app.Quit(ref missing, ref missing, ref missing);
        }
    }

    private static void RemoveInstallmentsTable(Document doc)
    {
        var table = FindInstallmentsTable(doc);
        table?.Delete();

        if (doc.Bookmarks.Exists("clear"))
        {
            var clearRange = doc.Bookmarks["clear"].Range;
            clearRange.Text = Environment.NewLine + Environment.NewLine;
            object clearName = "clear";
            doc.Bookmarks.Add(clearName.ToString(), clearRange);
        }
    }

    private static void RemoveUnusedInstallmentRows(Document doc, int installmentCount)
    {
        var table = FindInstallmentsTable(doc);
        if (table is null)
            return;

        var rowsToKeep = Math.Max(0, installmentCount);
        var firstInstallmentRow = 3; // 1: section header, 2: column header
        var maxUsedRow = firstInstallmentRow + rowsToKeep - 1;

        for (var row = table.Rows.Count; row >= firstInstallmentRow; row--)
        {
            if (row > maxUsedRow)
                table.Rows[row].Delete();
        }
    }

    private static Table? FindInstallmentsTable(Document doc)
    {
        foreach (var marker in new[] { "aa1", "dat1", "dos1" })
        {
            if (!doc.Bookmarks.Exists(marker))
                continue;

            var range = doc.Bookmarks[marker].Range;
            if (range.Tables.Count > 0)
                return range.Tables[1];
        }

        return null;
    }

    public void OpenDocumentInWord(string docxPath)
    {
        if (!File.Exists(docxPath))
            throw new FileNotFoundException("Contract DOCX not found.", docxPath);

        Process.Start(new ProcessStartInfo
        {
            FileName = docxPath,
            UseShellExecute = true
        });
    }
}
