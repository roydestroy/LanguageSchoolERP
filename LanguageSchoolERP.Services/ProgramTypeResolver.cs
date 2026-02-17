using LanguageSchoolERP.Core.Models;

namespace LanguageSchoolERP.Services;

public static class ProgramTypeResolver
{
    private const string MappingErrorMessage = "This program configuration can't be mapped to a legacy ProgramType. Please adjust flags or update legacy mapping.";

    public static bool TryResolveLegacyType(StudyProgram? program, out ProgramType legacyType, out string? errorMessage)
    {
        legacyType = ProgramType.LanguageSchool;
        errorMessage = null;

        if (program is null)
        {
            errorMessage = "Παρακαλώ επιλέξτε πρόγραμμα.";
            return false;
        }

        var candidates = new List<ProgramType>();

        if (program.HasBooks)
        {
            candidates.Add(ProgramType.LanguageSchool);
        }

        if (program.HasTransport)
        {
            candidates.Add(ProgramType.StudyLab);
        }

        if (!program.HasBooks && !program.HasTransport)
        {
            candidates.Add(ProgramType.EuroLab);
        }

        if (candidates.Count != 1)
        {
            errorMessage = MappingErrorMessage;
            return false;
        }

        legacyType = candidates[0];
        return true;
    }
}
