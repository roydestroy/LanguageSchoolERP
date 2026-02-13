namespace LanguageSchoolERP.Services
{
    public record ReceiptPrintData(
        int ReceiptNumber,
        DateTime IssueDate,
        string StudentName,
        string StudentPhone,
        string StudentEmail,
        decimal Amount,
        string PaymentMethod,
        string ProgramLabel,
        string AcademicYear,
        string Notes
    );
}
