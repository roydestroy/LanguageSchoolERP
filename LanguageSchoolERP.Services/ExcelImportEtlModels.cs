using System.Globalization;

namespace LanguageSchoolERP.Services;

public sealed record ExcelImportParseRow(
    string SheetName,
    int RowNumber,
    string StudentFullName,
    string? StudentPhone,
    string? FatherPhone,
    string? MotherPhone,
    string AcademicYearLabel,
    string ProgramName,
    string LevelOrClass,
    decimal AgreementTotal,
    decimal DownPayment,
    decimal TransportationMonthlyCost,
    decimal StudyLabMonthlyCost,
    bool HasTransportationColumn,
    bool HasStudyLabColumn,
    bool HasBooksColumn,
    bool IsDiscontinued,
    IReadOnlyList<ExcelMonthlyPaymentSignal> MonthlyPayments,
    decimal? ConfirmedCollectedAmount,
    DateTime? PaymentDate,
    string SourceNote);

public sealed record ExcelMonthlyPaymentSignal(
    string MonthLabel,
    DateTime PaymentDate,
    decimal Amount);

public sealed record ExcelImportRowError(string SheetName, int RowNumber, string Message);

public sealed record ExcelImportParseResult(
    IReadOnlyList<ExcelImportParseRow> Rows,
    IReadOnlyList<ExcelImportRowError> Errors);

public sealed class ExcelImportSummary
{
    public int InsertedStudents { get; set; }
    public int InsertedAcademicPeriods { get; set; }
    public int InsertedPrograms { get; set; }
    public int InsertedEnrollments { get; set; }
    public int UpdatedEnrollments { get; set; }
    public int InsertedPayments { get; set; }
    public int SkippedRows { get; set; }
    public int ErrorRows { get; set; }

    public string ToLogLine()
        => string.Create(
            CultureInfo.InvariantCulture,
            $"Summary: students+{InsertedStudents}, periods+{InsertedAcademicPeriods}, programs+{InsertedPrograms}, enrollments+{InsertedEnrollments}/{UpdatedEnrollments} (insert/update), payments+{InsertedPayments}, skipped={SkippedRows}, errors={ErrorRows}.");
}

public interface IExcelWorkbookParser
{
    Task<ExcelImportParseResult> ParseAsync(
        string workbookPath,
        string defaultProgramName,
        CancellationToken cancellationToken);
}
