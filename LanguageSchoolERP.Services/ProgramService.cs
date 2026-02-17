using LanguageSchoolERP.Core.Models;
using LanguageSchoolERP.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageSchoolERP.Services;

public class ProgramService : IProgramService
{
    private readonly DbContextFactory _dbContextFactory;

    public ProgramService(DbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<List<StudyProgram>> GetAllAsync(CancellationToken ct)
    {
        await using var db = _dbContextFactory.Create();
        return await db.Programs
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task<StudyProgram?> GetByIdAsync(int id, CancellationToken ct)
    {
        await using var db = _dbContextFactory.Create();
        return await db.Programs
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<StudyProgram> CreateAsync(StudyProgram program, CancellationToken ct)
    {
        await using var db = _dbContextFactory.Create();
        var name = await ValidateAndNormalizeNameAsync(db, program.Name, null, ct);

        program.Name = name;
        db.Programs.Add(program);
        await db.SaveChangesAsync(ct);
        return program;
    }

    public async Task UpdateAsync(StudyProgram program, CancellationToken ct)
    {
        await using var db = _dbContextFactory.Create();
        var existing = await db.Programs.FirstOrDefaultAsync(p => p.Id == program.Id, ct);
        if (existing is null)
        {
            throw new InvalidOperationException("Το πρόγραμμα δεν βρέθηκε.");
        }

        var name = await ValidateAndNormalizeNameAsync(db, program.Name, program.Id, ct);
        existing.Name = name;
        existing.HasTransport = program.HasTransport;
        existing.HasStudyLab = program.HasStudyLab;
        existing.HasBooks = program.HasBooks;

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        await using var db = _dbContextFactory.Create();
        var existing = await db.Programs.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (existing is null)
        {
            return;
        }

        db.Programs.Remove(existing);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException("Δεν μπορείτε να διαγράψετε πρόγραμμα που χρησιμοποιείται από εγγραφές.");
        }
    }

    private static async Task<string> ValidateAndNormalizeNameAsync(SchoolDbContext db, string? name, int? currentId, CancellationToken ct)
    {
        var normalized = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Το όνομα προγράμματος είναι υποχρεωτικό.");
        }

        if (normalized.Length > 200)
        {
            throw new InvalidOperationException("Το όνομα προγράμματος δεν μπορεί να υπερβαίνει τους 200 χαρακτήρες.");
        }

        var existingNames = await db.Programs
            .AsNoTracking()
            .Where(p => p.Id != currentId)
            .Select(p => p.Name)
            .ToListAsync(ct);

        var duplicateExists = existingNames.Any(existingName =>
            string.Equals(existingName?.Trim(), normalized, StringComparison.OrdinalIgnoreCase));

        if (duplicateExists)
        {
            throw new InvalidOperationException("Υπάρχει ήδη πρόγραμμα με το ίδιο όνομα.");
        }

        return normalized;
    }
}
