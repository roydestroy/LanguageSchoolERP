using LanguageSchoolERP.Core.Models;

namespace LanguageSchoolERP.Services;

public interface IProgramService
{
    Task<List<StudyProgram>> GetAllAsync(CancellationToken ct);
    Task<StudyProgram?> GetByIdAsync(int id, CancellationToken ct);
    Task<StudyProgram> CreateAsync(StudyProgram program, CancellationToken ct);
    Task UpdateAsync(StudyProgram program, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}
