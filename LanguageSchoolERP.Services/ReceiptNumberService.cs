using LanguageSchoolERP.Core.Models;
using LanguageSchoolERP.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace LanguageSchoolERP.Services;

public class ReceiptNumberService
{
    private readonly DbContextFactory _dbFactory;

    public ReceiptNumberService(DbContextFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<int> GetNextReceiptNumberAsync(Guid academicPeriodId)
    {
        using var db = _dbFactory.Create();
        DbSeeder.EnsureSeeded(db);

        using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

        var counter = await db.ReceiptCounters
            .FirstOrDefaultAsync(c => c.AcademicPeriodId == academicPeriodId);

        if (counter == null)
        {
            counter = new ReceiptCounter
            {
                AcademicPeriodId = academicPeriodId,
                NextReceiptNumber = 1
            };

            db.ReceiptCounters.Add(counter);
            await db.SaveChangesAsync();
        }


        var number = counter.NextReceiptNumber;
        counter.NextReceiptNumber++;

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return number;
    }
}
