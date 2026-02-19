namespace LanguageSchoolERP.App.ViewModels;

public class ProgramStatisticsRowVm
{
    public string ProgramName { get; init; } = string.Empty;
    public int StudentsCount { get; init; }
    public int EnrollmentsCount { get; init; }
    public decimal AgreementTotal { get; init; }
    public decimal CollectedTotal { get; init; }
    public int DiscontinuedCount { get; init; }
    public decimal LostAmount { get; init; }
    public decimal OutstandingTotal { get; init; }
}
