using Microsoft.EntityFrameworkCore;
using LanguageSchoolERP.Core.Models;

namespace LanguageSchoolERP.Data;

public class SchoolDbContext : DbContext
{
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ContractTemplate> ContractTemplates => Set<ContractTemplate>();
    public DbSet<AcademicPeriod> AcademicPeriods => Set<AcademicPeriod>();
    public DbSet<ReceiptCounter> ReceiptCounters => Set<ReceiptCounter>();

    public SchoolDbContext(DbContextOptions<SchoolDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Receipt>()
            .HasOne(r => r.Payment)
            .WithMany(p => p.Receipts)
            .HasForeignKey(r => r.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReceiptCounter>()
            .HasIndex(x => x.AcademicPeriodId)
            .IsUnique();

        modelBuilder.Entity<Contract>()
            .HasOne(c => c.Student)
            .WithMany(s => s.Contracts)
            .HasForeignKey(c => c.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Contract>()
            .HasOne(c => c.Enrollment)
            .WithMany(e => e.Contracts)
            .HasForeignKey(c => c.EnrollmentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Contract>()
            .HasOne(c => c.Template)
            .WithMany(t => t.Contracts)
            .HasForeignKey(c => c.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
