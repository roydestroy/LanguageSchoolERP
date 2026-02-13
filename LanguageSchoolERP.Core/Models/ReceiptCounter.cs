namespace LanguageSchoolERP.Core.Models;

public class ReceiptCounter
{
    public int ReceiptCounterId { get; set; }

    public Guid AcademicPeriodId { get; set; }
    public AcademicPeriod AcademicPeriod { get; set; }

    public int NextReceiptNumber { get; set; } = 1;
}
