using LanguageSchoolERP.Core.Models;
using LanguageSchoolERP.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace LanguageSchoolERP.Services;

public static class DbSeeder
{
    public static void EnsureSeeded(SchoolDbContext db)
    {
        if (!db.AcademicPeriods.Any())
        {
            db.AcademicPeriods.Add(new AcademicPeriod { Name = "2024-2025", IsCurrent = false });
            db.AcademicPeriods.Add(new AcademicPeriod { Name = "2025-2026", IsCurrent = true });
            db.SaveChanges();
        }
        // Ensure there is exactly 1 receipt counter per academic period
        var periods = db.AcademicPeriods.AsNoTracking().ToList();

        foreach (var p in periods)
        {
            bool exists = db.ReceiptCounters.Any(rc => rc.AcademicPeriodId == p.AcademicPeriodId);
            if (!exists)
            {
                db.ReceiptCounters.Add(new ReceiptCounter
                {
                    AcademicPeriodId = p.AcademicPeriodId,
                    NextReceiptNumber = 1
                });
            }
        }

        foreach (var p in db.AcademicPeriods.ToList())
        {
            var counter = db.ReceiptCounters.FirstOrDefault(rc => rc.AcademicPeriodId == p.AcademicPeriodId);
            if (counter == null) continue;

            // Find max receipt number for this academic year
            var maxReceiptNumber = db.Receipts
                .Where(r => r.Payment.Enrollment.AcademicPeriodId == p.AcademicPeriodId)
                .Select(r => (int?)r.ReceiptNumber)
                .Max() ?? 0;

            var desiredNext = maxReceiptNumber + 1;

            if (counter.NextReceiptNumber < desiredNext)
                counter.NextReceiptNumber = desiredNext;
        }

        db.SaveChanges();
    }
}
