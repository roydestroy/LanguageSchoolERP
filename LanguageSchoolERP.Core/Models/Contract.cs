namespace LanguageSchoolERP.Core.Models;

public class Contract
{
    public Guid ContractId { get; set; } = Guid.NewGuid();

    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public Guid EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = null!;

    public Guid TemplateId { get; set; }
    public ContractTemplate Template { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string? PdfPath { get; set; }
    public string DataJson { get; set; } = "{}";
}
