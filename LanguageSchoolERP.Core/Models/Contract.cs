namespace LanguageSchoolERP.Core.Models;

public class Contract
{
    public Guid ContractId { get; set; } = Guid.NewGuid();

    public Guid EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; }

    public string PdfPath { get; set; } = "";
    public string DocxPath { get; set; } = "";
}
