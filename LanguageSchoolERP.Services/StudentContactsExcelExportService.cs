using System.Runtime.InteropServices;
using Excel = Microsoft.Office.Interop.Excel;

namespace LanguageSchoolERP.Services;

public sealed class StudentContactsExcelExportService
{
    public void Export(string outputPath, IReadOnlyList<StudentContactsExportRow> rows, IReadOnlyList<string> headers)
    {
        if (rows.Count == 0)
            throw new InvalidOperationException("Δεν υπάρχουν δεδομένα για εξαγωγή.");

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        Excel.Application? app = null;
        Excel.Workbook? workbook = null;
        Excel.Worksheet? worksheet = null;

        try
        {
            app = new Excel.Application
            {
                DisplayAlerts = false,
                Visible = false
            };

            workbook = app.Workbooks.Add();
            worksheet = (Excel.Worksheet)workbook.Worksheets[1];
            worksheet.Name = "Επαφές";

            for (var i = 0; i < headers.Count; i++)
            {
                worksheet.Cells[1, i + 1] = headers[i];
            }

            for (var row = 0; row < rows.Count; row++)
            {
                var values = rows[row].Values;
                for (var column = 0; column < values.Count; column++)
                {
                    worksheet.Cells[row + 2, column + 1] = values[column];
                }
            }

            var headerRange = worksheet.Range[worksheet.Cells[1, 1], worksheet.Cells[1, headers.Count]];
            headerRange.Font.Bold = true;
            headerRange.Interior.Color = 0xE6E6E6;
            worksheet.Columns.AutoFit();

            workbook.SaveAs(outputPath, Excel.XlFileFormat.xlOpenXMLWorkbook);
        }
        finally
        {
            try { workbook?.Close(false); } catch { }
            try { app?.Quit(); } catch { }

            if (worksheet is not null) Marshal.ReleaseComObject(worksheet);
            if (workbook is not null) Marshal.ReleaseComObject(workbook);
            if (app is not null) Marshal.ReleaseComObject(app);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
}

public sealed class StudentContactsExportRow
{
    public required IReadOnlyList<string> Values { get; init; }
}
