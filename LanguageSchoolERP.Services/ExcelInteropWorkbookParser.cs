using System.Globalization;
using System.Runtime.InteropServices;
using Excel = Microsoft.Office.Interop.Excel;

namespace LanguageSchoolERP.Services;

public sealed class ExcelInteropWorkbookParser : IExcelWorkbookParser
{
    private static readonly CultureInfo ElGr = new("el-GR");
    private static readonly string[] MonthHints = ["ΣΕΠ", "ΟΚΤ", "ΝΟΕ", "ΔΕΚ", "ΙΑΝ", "ΦΕΒ", "ΜΑΡ", "ΑΠΡ", "ΜΑΙ", "ΙΟΥΝ"];

    public Task<ExcelImportParseResult> ParseAsync(
        string workbookPath,
        string defaultProgramName,
        CancellationToken cancellationToken)
    {
        return Task.Run(() => ParseInternal(workbookPath, defaultProgramName, cancellationToken), cancellationToken);
    }

    private static ExcelImportParseResult ParseInternal(string workbookPath, string defaultProgramName, CancellationToken ct)
    {
        var rows = new List<ExcelImportParseRow>();
        var errors = new List<ExcelImportRowError>();

        Excel.Application? app = null;
        Excel.Workbook? workbook = null;

        try
        {
            app = new Excel.Application { DisplayAlerts = false, Visible = false };
            workbook = app.Workbooks.Open(workbookPath, ReadOnly: true);

            foreach (Excel.Worksheet sheet in workbook.Worksheets)
            {
                try
                {
                    if (ct.IsCancellationRequested)
                        return new ExcelImportParseResult(rows, errors);

                    var used = sheet.UsedRange;
                    var rowCount = used.Rows.Count;
                    var colCount = used.Columns.Count;

                    if (rowCount < 2 || colCount < 1)
                        continue;

                    var headerInfo = FindHeaderRow(used, rowCount, colCount);
                    if (headerInfo is null)
                    {
                        errors.Add(new ExcelImportRowError(sheet.Name, 1, "Δεν βρέθηκε στήλη ονόματος μαθητή."));
                        continue;
                    }

                    var (headerRow, headerMap) = headerInfo.Value;

                    var fullNameCol = FindColumn(headerMap, "ΟΝΟΜΑ");
                    var studentPhoneCol = FindColumn(headerMap, "ΣΤΑΘΕΡΟ");
                    var fatherPhoneCol = FindColumn(headerMap, "ΜΠΑΜΠΑ");
                    var motherPhoneCol = FindColumn(headerMap, "ΜΑΜΑ");
                    var yearCol = FindColumn(headerMap, "ΑΚΑΔΗΜ", "ΕΤΟΣ");
                    var programCol = FindColumn(headerMap, "ΠΡΟΓΡ");
                    var agreementCol = FindAgreementColumn(headerMap);
                    var downPaymentCol = FindColumn(headerMap, "ΠΡΟΚ", "DOWN");
                    var transportationCol = FindColumn(headerMap, "ΜΕΤΑΦΟΡ");
                    var discontinuedCol = FindColumn(headerMap, "ΔΙΑΚΟΠ", "STOP", "DISCONT");
                    var collectionCol = FindColumn(headerMap, "ΕΙΣΠΡΑΞ");
                    var paymentDateCol = FindColumn(headerMap, "ΗΜΕΡ", "DATE");

                    var monthCols = headerMap
                        .Where(kvp => MonthHints.Any(m => kvp.Value.Contains(m, StringComparison.OrdinalIgnoreCase)))
                        .Select(kvp => new { kvp.Key, kvp.Value })
                        .ToList();

                    string? previousStudentPhone = null;
                    string? previousFatherPhone = null;
                    string? previousMotherPhone = null;

                    for (var row = headerRow + 1; row <= rowCount; row++)
                    {
                        if (ct.IsCancellationRequested)
                            return new ExcelImportParseResult(rows, errors);

                        var fullName = ReadText(used.Cells[row, fullNameCol!.Value]);
                        if (string.IsNullOrWhiteSpace(fullName))
                            continue;

                        if (fullName.Contains("ΣΥΝΟΛ", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var yearLabel = yearCol.HasValue
                            ? ReadText(used.Cells[row, yearCol.Value])
                            : string.Empty;

                        if (string.IsNullOrWhiteSpace(yearLabel))
                            yearLabel = TryExtractAcademicYearLabel(sheet.Name) ?? string.Empty;

                        if (string.IsNullOrWhiteSpace(yearLabel))
                        {
                            errors.Add(new ExcelImportRowError(sheet.Name, row, "Δεν ήταν δυνατή η ανάγνωση ακαδημαϊκού έτους."));
                            continue;
                        }

                        var programName = programCol.HasValue
                            ? ReadText(used.Cells[row, programCol.Value])
                            : string.Empty;

                        if (string.IsNullOrWhiteSpace(programName))
                            programName = defaultProgramName;

                        var agreementTotal = agreementCol.HasValue ? ReadDecimal(used.Cells[row, agreementCol.Value]) : 0m;
                        var downPayment = downPaymentCol.HasValue ? ReadDecimal(used.Cells[row, downPaymentCol.Value]) : 0m;
                        var transportationMonthlyCost = transportationCol.HasValue ? ReadDecimal(used.Cells[row, transportationCol.Value]) : 0m;
                        var isDiscontinued = discontinuedCol.HasValue && IsTruthyYes(ReadText(used.Cells[row, discontinuedCol.Value]));
                        var collection = collectionCol.HasValue ? ReadDecimal(used.Cells[row, collectionCol.Value]) : 0m;

                        var normalizedYearLabel = NormalizeAcademicYearLabel(yearLabel);
                        var monthlySignals = new List<ExcelMonthlyPaymentSignal>();
                        decimal monthTotal = 0m;
                        foreach (var monthCol in monthCols)
                        {
                            var amount = ReadDecimal(used.Cells[row, monthCol.Key]);
                            if (amount <= 0m)
                                continue;

                            monthTotal += amount;
                            DateTime paymentDateForMonth;
                            if (TryBuildMonthPaymentDate(normalizedYearLabel, monthCol.Value, out paymentDateForMonth))
                            {
                                monthlySignals.Add(new ExcelMonthlyPaymentSignal(monthCol.Value, paymentDateForMonth, amount));
                            }
                        }

                        var confirmedCollected = collection > 0m ? collection : monthTotal > 0m ? monthTotal : null;
                        var paymentDate = paymentDateCol.HasValue ? ReadDate(used.Cells[row, paymentDateCol.Value]) : null;

                        var sourceStudentPhone = studentPhoneCol.HasValue ? NormalizePhone(ReadText(used.Cells[row, studentPhoneCol.Value])) : null;
                        var sourceFatherPhone = fatherPhoneCol.HasValue ? NormalizePhone(ReadText(used.Cells[row, fatherPhoneCol.Value])) : null;
                        var sourceMotherPhone = motherPhoneCol.HasValue ? NormalizePhone(ReadText(used.Cells[row, motherPhoneCol.Value])) : null;

                        var isSiblingRow = fullName.Contains(">>", StringComparison.Ordinal);
                        var normalizedStudentName = NormalizeStudentNameOrder(fullName.Replace(">>", string.Empty, StringComparison.Ordinal));

                        if (isSiblingRow)
                        {
                            sourceStudentPhone = previousStudentPhone;
                            sourceFatherPhone = previousFatherPhone;
                            sourceMotherPhone = previousMotherPhone;
                        }
                        else
                        {
                            previousStudentPhone = sourceStudentPhone;
                            previousFatherPhone = sourceFatherPhone;
                            previousMotherPhone = sourceMotherPhone;
                        }

                        rows.Add(new ExcelImportParseRow(
                            sheet.Name,
                            row,
                            normalizedStudentName,
                            sourceStudentPhone,
                            sourceFatherPhone,
                            sourceMotherPhone,
                            normalizedYearLabel,
                            programName.Trim(),
                            agreementTotal,
                            downPayment,
                            transportationMonthlyCost,
                            isDiscontinued,
                            monthlySignals,
                            confirmedCollected,
                            paymentDate,
                            Path.GetFileName(workbookPath)));
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(sheet);
                }
            }
        }
        finally
        {
            if (workbook is not null)
            {
                workbook.Close(false);
                Marshal.ReleaseComObject(workbook);
            }

            if (app is not null)
            {
                app.Quit();
                Marshal.ReleaseComObject(app);
            }
        }

        return new ExcelImportParseResult(rows, errors);
    }

    private static (int HeaderRow, Dictionary<int, string> HeaderMap)? FindHeaderRow(Excel.Range used, int rowCount, int colCount)
    {
        var maxHeaderScanRow = Math.Min(rowCount, 12);

        for (var row = 1; row <= maxHeaderScanRow; row++)
        {
            var headerMap = BuildHeaderMap(used, row, colCount);
            if (headerMap.Count == 0)
                continue;

            var hasName = FindColumn(headerMap, "ΟΝΟΜΑ") is not null;
            var hasFinancialSignal = FindColumn(headerMap, "ΣΥΜΦΩΝ", "ΠΡΟΚ", "ΕΙΣΠΡΑΞ") is not null
                                     || headerMap.Any(kvp => MonthHints.Any(m => kvp.Value.Contains(m, StringComparison.OrdinalIgnoreCase)));

            if (hasName && hasFinancialSignal)
                return (row, headerMap);
        }

        return null;
    }

    private static Dictionary<int, string> BuildHeaderMap(Excel.Range used, int headerRow, int colCount)
    {
        var map = new Dictionary<int, string>();
        for (var col = 1; col <= colCount; col++)
        {
            var header = ReadText(used.Cells[headerRow, col]);
            if (!string.IsNullOrWhiteSpace(header))
                map[col] = NormalizeHeader(header);
        }

        return map;
    }


    private static int? FindAgreementColumn(IReadOnlyDictionary<int, string> headers)
    {
        // Prefer explicit agreement columns, avoid "ΣΥΜΦΩΝΗΤΙΚΟ" boolean/signature columns.
        foreach (var (col, header) in headers)
        {
            if (header.Contains("ΣΥΜΦΩΝΙΑ", StringComparison.OrdinalIgnoreCase)
                || header.Contains("AGREEMENT", StringComparison.OrdinalIgnoreCase)
                || header.Contains("TOTAL", StringComparison.OrdinalIgnoreCase))
            {
                return col;
            }
        }

        foreach (var (col, header) in headers)
        {
            if (header.Contains("ΣΥΜΦΩΝ", StringComparison.OrdinalIgnoreCase)
                && !header.Contains("ΣΥΜΦΩΝΗΤΙΚ", StringComparison.OrdinalIgnoreCase))
            {
                return col;
            }
        }

        return null;
    }

    private static int? FindColumn(IReadOnlyDictionary<int, string> headers, params string[] tokens)
    {
        foreach (var (col, header) in headers)
        {
            if (tokens.Any(t => header.Contains(t, StringComparison.OrdinalIgnoreCase)))
                return col;
        }

        return null;
    }

    private static string NormalizeHeader(string value)
        => value.Replace(" ", string.Empty, StringComparison.Ordinal).Trim().ToUpperInvariant();

    private static string ReadText(Excel.Range cell)
    {
        var value = cell?.Value2;
        return value?.ToString() ?? string.Empty;
    }

    private static decimal ReadDecimal(Excel.Range cell)
    {
        var txt = ReadText(cell).Trim();
        if (string.IsNullOrWhiteSpace(txt))
            return 0m;

        if (decimal.TryParse(txt, NumberStyles.Any, ElGr, out var d))
            return d;

        if (decimal.TryParse(txt, NumberStyles.Any, CultureInfo.InvariantCulture, out d))
            return d;

        return 0m;
    }

    private static DateTime? ReadDate(Excel.Range cell)
    {
        var raw = cell?.Value2;
        if (raw is double oa)
            return DateTime.FromOADate(oa);

        var txt = ReadText(cell).Trim();
        if (DateTime.TryParse(txt, ElGr, DateTimeStyles.None, out var dt))
            return dt;

        return null;
    }

    private static string NormalizeAcademicYearLabel(string value)
    {
        var txt = value.Trim();
        if (txt.Contains('-'))
            return txt;

        var digits = new string(txt.Where(char.IsDigit).ToArray());
        if (digits.Length >= 8)
            return $"{digits[..4]}-{digits.Substring(4, 4)}";

        return txt;
    }

    private static string? TryExtractAcademicYearLabel(string sheetName)
    {
        var digits = new string(sheetName.Where(c => char.IsDigit(c) || c == '-').ToArray());
        return string.IsNullOrWhiteSpace(digits) ? null : NormalizeAcademicYearLabel(digits);
    }


    private static bool TryBuildMonthPaymentDate(string academicYearLabel, string monthHeader, out DateTime paymentDate)
    {
        paymentDate = default;

        if (!TryParseAcademicYear(academicYearLabel, out var startYear, out var endYear))
            return false;

        var month = TryResolveMonthNumber(monthHeader);
        if (!month.HasValue)
            return false;

        var year = month is >= 9 and <= 12 ? startYear : endYear;
        paymentDate = new DateTime(year, month.Value, 1);
        return true;
    }

    private static int? TryResolveMonthNumber(string monthHeader)
    {
        var h = monthHeader.ToUpperInvariant();
        if (h.Contains("ΣΕΠ")) return 9;
        if (h.Contains("ΟΚΤ")) return 10;
        if (h.Contains("ΝΟΕ")) return 11;
        if (h.Contains("ΔΕΚ")) return 12;
        if (h.Contains("ΙΑΝ")) return 1;
        if (h.Contains("ΦΕΒ")) return 2;
        if (h.Contains("ΜΑΡ")) return 3;
        if (h.Contains("ΑΠΡ")) return 4;
        if (h.Contains("ΜΑΙ")) return 5;
        if (h.Contains("ΙΟΥΝ")) return 6;

        return null;
    }

    private static bool TryParseAcademicYear(string label, out int startYear, out int endYear)
    {
        startYear = 0;
        endYear = 0;

        var parts = label.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2
            && int.TryParse(parts[0], out startYear)
            && int.TryParse(parts[1], out endYear))
        {
            return true;
        }

        return false;
    }


    private static string NormalizeStudentNameOrder(string rawName)
    {
        var parts = rawName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (parts.Count < 2)
            return rawName.Trim();

        // Source files use "SURNAME FIRSTNAME ..."; store as "FIRSTNAME ... SURNAME".
        var surname = parts[0];
        parts.RemoveAt(0);
        parts.Add(surname);

        return string.Join(' ', parts);
    }


    private static bool IsTruthyYes(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        return normalized is "ΝΑΙ" or "Ν" or "ΝΕ" or "YES" or "Y" or "TRUE" or "T"
            || normalized.StartsWith("NAI", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("ΔΙΑΚ", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("STOP", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return string.IsNullOrWhiteSpace(digits) ? null : digits;
    }
}
