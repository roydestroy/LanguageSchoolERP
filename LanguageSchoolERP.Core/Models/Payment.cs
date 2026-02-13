using System.Collections.Generic;
namespace LanguageSchoolERP.Core.Models;

public class Payment
{
    public Guid PaymentId { get; set; } = Guid.NewGuid();

    public Guid EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; }

    public DateTime PaymentDate { get; set; } = DateTime.Now;
    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; }
    public string Notes { get; set; } = "";

    public ICollection<Receipt> Receipts { get; set; } = new List<Receipt>(); 
}
