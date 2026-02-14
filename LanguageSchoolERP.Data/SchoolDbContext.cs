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

        modelBuilder.Entity<ContractTemplate>()
            .Property(x => x.Name)
            .HasMaxLength(200);

        modelBuilder.Entity<ContractTemplate>()
            .Property(x => x.BranchKey)
            .HasMaxLength(100);

        modelBuilder.Entity<ContractTemplate>()
            .Property(x => x.TemplateRelativePath)
            .HasMaxLength(400);
    }
}
