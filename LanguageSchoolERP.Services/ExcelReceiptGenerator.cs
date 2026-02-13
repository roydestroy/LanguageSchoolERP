using System;
using System.IO;
using System.Runtime.InteropServices;
using Excel = Microsoft.Office.Interop.Excel;

namespace LanguageSchoolERP.Services;

public class ExcelReceiptGenerator
{
    public string GenerateReceiptPdf(string templatePath, string outputPdfPath, ReceiptPrintData data)
    {
        if (!File.Exists(templatePath))
            throw new FileNotFoundException("Receipt template not found.", templatePath);

        var outDir = Path.GetDirectoryName(outputPdfPath);
        if (!string.IsNullOrWhiteSpace(outDir))
            Directory.CreateDirectory(outDir);

        Excel.Application? app = null;
        Excel.Workbook? wb = null;
        Excel.Worksheet? ws = null;
        var amountText = GreekMoneyTextService.AmountToGreekText(data.Amount);

        try
        {
            app = new Excel.Application
            {
                DisplayAlerts = false,
                Visible = false
            };

            wb = app.Workbooks.Open(templatePath);
            ws = (Excel.Worksheet)wb.Worksheets[1];

            // Fill template cells (EDIT these to match your .xlsx)
            ws.Range["C16"].Value2 = data.StudentName;
            ws.Range["D14"].Value2 = data.ReceiptNumber;
            ws.Range["H13"].Value2 = data.IssueDate.ToString("dd/MM/yyyy");
            ws.Range["H18"].Value2 = data.Amount.ToString("0.00");
           // ws.Range["B9"].Value2 = data.PaymentMethod;
            ws.Range["C20"].Value2 = data.Notes ?? "";
            ws.Range["C19"].Value2 = amountText;

            wb.ExportAsFixedFormat(
                Excel.XlFixedFormatType.xlTypePDF,
                outputPdfPath
            );

            return outputPdfPath;
        }
        finally
        {
            // Close first
            try { if (wb != null) wb.Close(false); } catch { /* ignore */ }
            try { if (app != null) app.Quit(); } catch { /* ignore */ }

            // Release COM objects (reverse order)
            if (ws != null) Marshal.ReleaseComObject(ws);
            if (wb != null) Marshal.ReleaseComObject(wb);
            if (app != null) Marshal.ReleaseComObject(app);

            ws = null; wb = null; app = null;

            // Help prevent Excel.exe staying alive
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
}
