namespace LanguageSchoolERP.Core.Models;

public class StudyProgram
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool HasTransport { get; set; }

    public bool HasStudyLab { get; set; }

    public bool HasBooks { get; set; }
}
