namespace LanguageSchoolERP.Core.Models;

public sealed class ContractPayload
{
    public Guid ContractId { get; init; }
    public Guid StudentId { get; init; }
    public Guid EnrollmentId { get; init; }
    public string AcademicYear { get; init; } = "";
    public string BranchKey { get; init; } = "";

    public string StudentFullName { get; init; } = "";
    public string StudentFirstName { get; init; } = "";
    public string StudentLastName { get; init; } = "";
    public string GuardianFullName { get; init; } = "";
    public string ProgramNameUpper { get; init; } = "";
    public string ProgramTitleUpperWithExtras { get; init; } = "";

    public decimal AgreementTotal { get; init; }
    public decimal DownPayment { get; init; }

    public bool IncludesTransportation { get; init; }
    public decimal? TransportationMonthlyPrice { get; init; }
    public bool IncludesStudyLab { get; init; }
    public decimal? StudyLabMonthlyPrice { get; init; }

    public int InstallmentCount { get; init; }
    public DateTime? InstallmentStartMonth { get; init; }
    public int InstallmentDayOfMonth { get; init; }

    public DateTime CreatedAt { get; init; }
}
