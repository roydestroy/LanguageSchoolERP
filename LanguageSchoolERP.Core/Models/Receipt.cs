namespace LanguageSchoolERP.Core.Models;

public class Receipt
{
    public Guid ReceiptId { get; set; } = Guid.NewGuid();

    public int ReceiptNumber { get; set; }
    public DateTime IssueDate { get; set; } = DateTime.Now;

    public Guid PaymentId { get; set; }
    public Payment Payment { get; set; }

    public string PdfPath { get; set; } = "";

    public bool Voided { get; set; }
    public string VoidReason { get; set; } = "";
}
