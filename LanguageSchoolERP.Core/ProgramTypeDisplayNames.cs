namespace LanguageSchoolERP.Core.Models;

public static class ProgramTypeDisplayNames
{
    public static string ToDisplayName(this ProgramType programType) => programType switch
    {
        ProgramType.LanguageSchool => "Ξένες Γλώσσες",
        ProgramType.StudyLab => "Αίθουσα Μελέτης",
        ProgramType.EuroLab => "Πληροφορική",
        _ => programType.ToString()
    };
}
