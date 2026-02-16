namespace LanguageSchoolERP.Core.Models;

public static class ProgramTypeDisplayNames
{
    public static string ToDisplayName(this ProgramType programType) => programType switch
    {
        ProgramType.LanguageSchool => "Foreign Languages",
        ProgramType.StudyLab => "School Study",
        ProgramType.EuroLab => "Computers",
        _ => programType.ToString()
    };
}
