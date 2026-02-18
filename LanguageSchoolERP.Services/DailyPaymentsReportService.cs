using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace LanguageSchoolERP.Services;

public sealed class DailyPaymentsReportService
{
    public void GenerateDailyPaymentsPdf(string outputPath, DateTime day, IReadOnlyList<DailyPaymentsReportItem> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory);

        using var document = new PdfDocument();
        var titleFont = new XFont("Arial", 16, XFontStyle.Bold);
        var headerFont = new XFont("Arial", 10, XFontStyle.Bold);
        var bodyFont = new XFont("Arial", 9, XFontStyle.Regular);

        const double margin = 32;
        const double rowHeight = 16;
        const double headerHeight = 18;

        var columns = new[]
        {
            new ColumnDefinition("Ώρα", 50),
            new ColumnDefinition("Μαθητής", 120),
            new ColumnDefinition("Πρόγραμμα", 100),
            new ColumnDefinition("Ποσό", 65),
            new ColumnDefinition("Μέθοδος", 85),
            new ColumnDefinition("Σημειώσεις", 115)
        };

        PdfPage? page = null;
        XGraphics? gfx = null;
        var y = 0d;

        void StartNewPage(bool includeTitle)
        {
            gfx?.Dispose();
            page = document.AddPage();
            page.Size = PageSize.A4;
            gfx = XGraphics.FromPdfPage(page);
            y = margin;

            if (includeTitle)
            {
                gfx.DrawString($"Πληρωμές Ημέρας - {day:dd/MM/yyyy}", titleFont, XBrushes.Black, new XRect(margin, y, page.Width - margin * 2, 24), XStringFormats.TopLeft);
                y += 30;

                var total = items.Sum(x => x.Amount);
                gfx.DrawString($"Σύνολο εισπράξεων: {total:0.00} €", headerFont, XBrushes.Black, new XRect(margin, y, page.Width - margin * 2, 16), XStringFormats.TopLeft);
                y += 24;
            }

            DrawHeader(gfx, headerFont, margin, y, columns);
            y += headerHeight;
        }

        StartNewPage(includeTitle: true);

        foreach (var item in items)
        {
            if (page is null || gfx is null)
                break;

            if (y > page.Height - margin - rowHeight)
            {
                StartNewPage(includeTitle: false);
            }

            DrawRow(gfx, bodyFont, margin, y, columns, item);
            y += rowHeight;
        }

        gfx?.Dispose();
        document.Save(outputPath);
    }

    private static void DrawHeader(XGraphics gfx, XFont font, double x, double y, IReadOnlyList<ColumnDefinition> columns)
    {
        var offset = x;
        foreach (var column in columns)
        {
            var rect = new XRect(offset, y, column.Width, 16);
            gfx.DrawRectangle(XPens.DarkGray, XBrushes.LightGray, rect);
            gfx.DrawString(column.Title, font, XBrushes.Black, rect, XStringFormats.Center);
            offset += column.Width;
        }
    }

    private static void DrawRow(XGraphics gfx, XFont font, double x, double y, IReadOnlyList<ColumnDefinition> columns, DailyPaymentsReportItem item)
    {
        var values = new[]
        {
            item.Time,
            item.StudentName,
            item.ProgramName,
            $"{item.Amount:0.00} €",
            item.Method,
            item.Notes
        };

        var offset = x;
        for (var i = 0; i < columns.Count; i++)
        {
            var rect = new XRect(offset, y, columns[i].Width, 16);
            gfx.DrawRectangle(XPens.LightGray, rect);
            var textRect = new XRect(offset + 2, y + 1, columns[i].Width - 4, 14);
            gfx.DrawString(Truncate(values[i], columns[i].Width), font, XBrushes.Black, textRect, XStringFormats.TopLeft);
            offset += columns[i].Width;
        }
    }

    private static string Truncate(string text, double width)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var maxChars = Math.Max(4, (int)(width / 5));
        if (text.Length <= maxChars)
            return text;

        return text[..(maxChars - 1)] + "…";
    }

    private sealed record ColumnDefinition(string Title, double Width);
}

public sealed class DailyPaymentsReportItem
{
    public string Time { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}
