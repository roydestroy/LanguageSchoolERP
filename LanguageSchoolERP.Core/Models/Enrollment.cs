namespace LanguageSchoolERP.Core.Models;

public class Enrollment
{
    public int InstallmentCount { get; set; } = 0; // 0 means “not set / pay anytime”
    public DateTime? InstallmentStartMonth { get; set; } // store as first day of month

    public bool IncludesStudyLab { get; set; }
    public decimal? StudyLabMonthlyPrice { get; set; }

    public bool IncludesTransportation { get; set; }
    public decimal? TransportationMonthlyPrice { get; set; }

    public Guid EnrollmentId { get; set; } = Guid.NewGuid();

    public Guid StudentId { get; set; }
    public Student Student { get; set; }

    public Guid AcademicPeriodId { get; set; }
    public AcademicPeriod AcademicPeriod { get; set; }

    public ProgramType ProgramType { get; set; }

    public string LevelOrClass { get; set; } = "";

    public decimal AgreementTotal { get; set; }
    public decimal BooksAmount { get; set; }
    public decimal DownPayment { get; set; }

    public string Comments { get; set; } = "";
    public string Status { get; set; } = "Active";

    public ICollection<Payment> Payments { get; set; }
        = new List<Payment>();

    public ICollection<Contract> Contracts { get; set; }
        = new List<Contract>();
}
