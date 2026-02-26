using LanguageSchoolERP.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace LanguageSchoolERP.Services;

public class ReceiptNumberService
{
    private readonly DbContextFactory _dbFactory;

    public ReceiptNumberService(DbContextFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<int> GetNextReceiptNumberAsync(Guid enrollmentId)
    {
        using var strategyDb = _dbFactory.Create();
        var strategy = strategyDb.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var db = _dbFactory.Create();
            DbSeeder.EnsureSeeded(db);

            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            var enrollmentInfo = await db.Enrollments
                .AsNoTracking()
                .Where(e => e.EnrollmentId == enrollmentId)
                .Select(e => new { e.StudentId, e.AcademicPeriodId })
                .FirstOrDefaultAsync();

            if (enrollmentInfo is null)
                throw new InvalidOperationException("Enrollment not found.");

            // Numbering is per student per academic year.
            var maxReceiptForStudentYear = await db.Receipts
                .Where(r => r.Payment.Enrollment.StudentId == enrollmentInfo.StudentId
                         && r.Payment.Enrollment.AcademicPeriodId == enrollmentInfo.AcademicPeriodId)
                .Select(r => (int?)r.ReceiptNumber)
                .MaxAsync() ?? 0;

            var number = maxReceiptForStudentYear + 1;

            await tx.CommitAsync();
            return number;
        });
    }
}
