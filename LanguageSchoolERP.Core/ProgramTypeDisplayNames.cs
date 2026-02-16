namespace LanguageSchoolERP.Core.Models;

public static class ProgramTypeDisplayNames
{
    public static string ToDisplayName(this ProgramType programType) => programType switch
    {
        ProgramType.LanguageSchool => "Ξένες Γλώσσες",
        ProgramType.StudyLab => "Σχολική Μελέτη",
        ProgramType.EuroLab => "Υπολογιστές",
        _ => programType.ToString()
    };
}
