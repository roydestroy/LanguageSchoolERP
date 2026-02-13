namespace LanguageSchoolERP.Core.Models;

public class AcademicPeriod
{
    public Guid AcademicPeriodId { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "";
    public bool IsCurrent { get; set; }
}
