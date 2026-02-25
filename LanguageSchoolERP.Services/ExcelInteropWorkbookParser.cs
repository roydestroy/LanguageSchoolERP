using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
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
        return Task.Run(() => ParseInternal(workbookPath, defaultProgramName, cancellationToken));
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
                    var levelCol = FindLevelColumn(headerMap, defaultProgramName);
                    var agreementCol = FindAgreementColumn(headerMap);
                    var downPaymentCol = FindColumn(headerMap, "ΠΡΟΚ", "DOWN");
                    var transportationCol = FindTransportationColumn(headerMap, headerRow);
                    var studyLabCol = FindStudyLabColumn(headerMap, headerRow);
                    var booksCol = FindBooksColumn(headerMap, headerRow);
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

                        var levelOrClass = levelCol.HasValue
                            ? ReadText(used.Cells[row, levelCol.Value]).Trim()
                            : string.Empty;

                        var programName = ResolveProgramName(
                            programCol.HasValue ? ReadText(used.Cells[row, programCol.Value]) : string.Empty,
                            levelOrClass,
                            defaultProgramName);

                        var agreementCell = agreementCol.HasValue ? (Excel.Range)used.Cells[row, agreementCol.Value] : null;
                        var agreementTotal = agreementCell is not null ? ReadDecimal(agreementCell) : 0m;
                        var installmentCount = agreementCell is not null ? TryExtractInstallmentCountFromAgreementComment(agreementCell) : 0;
                        DateTime? installmentStartMonth = installmentCount > 0 ? new DateTime(2025, 10, 1) : null;
                        var downPayment = downPaymentCol.HasValue ? ReadDecimal(used.Cells[row, downPaymentCol.Value]) : 0m;
                        var transportationMonthlyCost = transportationCol.HasValue ? ReadDecimal(used.Cells[row, transportationCol.Value]) : 0m;
                        var studyLabMonthlyCost = studyLabCol.HasValue ? ReadDecimal(used.Cells[row, studyLabCol.Value]) : 0m;
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
                            levelOrClass,
                            agreementTotal,
                            downPayment,
                            transportationMonthlyCost,
                            studyLabMonthlyCost,
                            transportationCol.HasValue,
                            studyLabCol.HasValue,
                            booksCol.HasValue,
                            isDiscontinued,
                            monthlySignals,
                            installmentCount,
                            installmentStartMonth,
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



    private static int? FindTransportationColumn(IReadOnlyDictionary<int, string> headers, int headerRow)
    {
        // Import variant uses row 3 headers for addons.
        if (headerRow == 3)
            return FindColumn(headers, "ΜΕΤΑΦΟΡΑ", "ΜΕΤΑΦΟΡ");

        return FindColumn(headers, "ΜΕΤΑΦΟΡ");
    }

    private static int? FindStudyLabColumn(IReadOnlyDictionary<int, string> headers, int headerRow)
    {
        if (headerRow == 3)
            return FindColumn(headers, "ΜΕΛΕΤΗ", "STUDYLAB", "STUDY");

        return FindColumn(headers, "ΜΕΛΕΤΗ", "STUDYLAB", "STUDY");
    }

    private static int? FindBooksColumn(IReadOnlyDictionary<int, string> headers, int headerRow)
    {
        if (headerRow == 3)
            return FindColumn(headers, "ΒΙΒΛΙΑ", "BOOK");

        return FindColumn(headers, "ΒΙΒΛΙΑ", "BOOK");
    }

    private static int? FindLevelColumn(IReadOnlyDictionary<int, string> headers, string defaultProgramName)
    {
        // Language enrollment workbooks use explicit level headers (commonly on row 3).
        // Do not treat column D as level/class unless one of these headers exists.
        var levelColumn = FindColumn(headers, "ΕΠΙΠΕΔΟ", "ΕΝΟΤΗΤΕΣ", "ΕΝΟΤΗΤ", "LEVEL");
        if (levelColumn.HasValue)
            return levelColumn;

        // Study support workbook (ΣΧΟΛΙΚΗ ΜΕΛΕΤΗ) stores class in a dedicated "ΤΑΞΗ" column.
        if (defaultProgramName.Contains("ΣΧΟΛΙΚΗ ΜΕΛΕΤΗ", StringComparison.OrdinalIgnoreCase))
            return FindColumn(headers, "ΤΑΞΗ", "ΤΑΞ", "CLASS");

        return null;
    }

    private static string ResolveProgramName(string explicitProgramName, string levelOrClass, string defaultProgramName)
    {
        var explicitNormalized = NormalizeProgramAlias(explicitProgramName);
        if (!string.IsNullOrWhiteSpace(explicitNormalized))
            return explicitNormalized;

        if (!defaultProgramName.Contains("ΣΧΟΛΙΚΗ ΜΕΛΕΤΗ", StringComparison.OrdinalIgnoreCase))
        {
            var mapped = MapLanguageProgramFromLevel(levelOrClass);
            if (!string.IsNullOrWhiteSpace(mapped))
                return mapped;
        }

        return defaultProgramName;
    }

    private static string? NormalizeProgramAlias(string? explicitProgramName)
    {
        if (string.IsNullOrWhiteSpace(explicitProgramName))
            return null;

        var normalized = explicitProgramName.Trim();
        if (normalized.Equals("KIDS", StringComparison.OrdinalIgnoreCase))
            return "ΑΓΓΛΙΚΗ ΓΛΩΣΣΑ";

        return normalized;
    }

    private static string? MapLanguageProgramFromLevel(string? levelOrClass)
    {
        if (string.IsNullOrWhiteSpace(levelOrClass))
            return null;

        var normalized = levelOrClass.Trim().ToUpperInvariant();
        if (normalized.StartsWith("KIDS", StringComparison.OrdinalIgnoreCase))
            return "ΑΓΓΛΙΚΗ ΓΛΩΣΣΑ";

        var firstChar = normalized[0];

        if (firstChar is 'E' or 'Ε')
            return "ΑΓΓΛΙΚΗ ΓΛΩΣΣΑ";

        if (firstChar is 'F' or 'Φ')
            return "ΓΑΛΛΙΚΗ ΓΛΩΣΣΑ";

        if (firstChar is 'G' or 'Γ')
            return "ΓΕΡΜΑΝΙΚΗ ΓΛΩΣΣΑ";

        return null;
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

    private static int TryExtractInstallmentCountFromAgreementComment(Excel.Range cell)
    {
        var commentText = ReadCommentText(cell);
        if (string.IsNullOrWhiteSpace(commentText))
            return 0;

        // Example comments: 65+140*8, (65+140)*8, 65+8*74.
        // Installments are valid only in range 1..12 and may appear either before or after the multiply symbol.
        // Support parenthesized expressions like 65+(85+15)*8 where left side is not a plain number.
        var rightSideMatches = Regex.Matches(commentText, @"[\*xX×]\s*(\d+)");
        foreach (Match match in rightSideMatches)
        {
            if (!match.Success)
                continue;

            if (int.TryParse(match.Groups[1].Value, out var right) && IsValidInstallmentCount(right))
                return right;
        }

        var leftSideMatches = Regex.Matches(commentText, @"(\d+)\s*[\*xX×]");
        foreach (Match match in leftSideMatches)
        {
            if (!match.Success)
                continue;

            if (int.TryParse(match.Groups[1].Value, out var left) && IsValidInstallmentCount(left))
                return left;
        }

        return 0;
    }

    private static bool IsValidInstallmentCount(int value)
        => value > 0 && value < 13;

    private static string ReadCommentText(Excel.Range cell)
    {
        var comment = cell?.Comment;
        if (comment is null)
            return string.Empty;

        try
        {
            return comment.Text() ?? string.Empty;
        }
        finally
        {
            Marshal.ReleaseComObject(comment);
        }
    }

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
