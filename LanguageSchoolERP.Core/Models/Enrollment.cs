namespace LanguageSchoolERP.Core.Models;

public class Enrollment
{
    public int InstallmentCount { get; set; } = 0; // 0 means “not set / pay anytime”
    public DateTime? InstallmentStartMonth { get; set; } // store as first day of month

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
    public bool HasTransportation { get; set; }
    public decimal TransportationMonthlyFee { get; set; }   // decimal(18,2)

    public bool HasStudyLab { get; set; }
    public decimal StudyLabMonthlyFee { get; set; }         // decimal(18,2)

    public int InstallmentDayOfMonth { get; set; } = 1;

    public ICollection<Payment> Payments { get; set; }
        = new List<Payment>();

    public ICollection<Contract> Contracts { get; set; }
        = new List<Contract>();

}
