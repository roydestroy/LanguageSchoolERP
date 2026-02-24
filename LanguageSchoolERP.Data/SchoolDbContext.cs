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
    public DbSet<StudyProgram> Programs => Set<StudyProgram>();
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

        modelBuilder.Entity<StudyProgram>()
            .ToTable("Programs");

        modelBuilder.Entity<StudyProgram>()
            .Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        modelBuilder.Entity<StudyProgram>()
            .HasIndex(p => p.Name)
            .IsUnique();

        modelBuilder.Entity<AcademicPeriod>()
            .HasIndex(p => p.Name)
            .IsUnique();

        modelBuilder.Entity<Student>()
            .HasIndex(s => new { s.LastName, s.FirstName });

        modelBuilder.Entity<Enrollment>()
            .HasIndex(e => new { e.StudentId, e.AcademicPeriodId });

        modelBuilder.Entity<Enrollment>()
            .HasIndex(e => new { e.AcademicPeriodId, e.IsStopped });

        modelBuilder.Entity<Payment>()
            .HasIndex(p => p.PaymentDate);

        // -------------------------------------------------------
        // FIX: Explicit relationships to avoid multiple cascade paths
        // -------------------------------------------------------

        // Student -> Enrollments should cascade (reasonable default)
        modelBuilder.Entity<Enrollment>()
            .HasOne(e => e.Student)
            .WithMany(s => s.Enrollments)
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Student -> Contracts should NOT cascade, because:
        // Student -> Enrollments -> Contracts already cascades
        modelBuilder.Entity<Contract>()
            .HasOne(c => c.Student)
            .WithMany(s => s.Contracts)
            .HasForeignKey(c => c.StudentId)
            .OnDelete(DeleteBehavior.NoAction);

        // Enrollment -> Contracts can cascade
        modelBuilder.Entity<Contract>()
            .HasOne(c => c.Enrollment)
            .WithMany(e => e.Contracts)
            .HasForeignKey(c => c.EnrollmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // ContractTemplate -> Contracts can cascade
        modelBuilder.Entity<Contract>()
            .HasOne(c => c.ContractTemplate)
            .WithMany(t => t.Contracts)
            .HasForeignKey(c => c.ContractTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Enrollment>()
            .Property(x => x.TransportationMonthlyFee)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<Enrollment>()
            .Property(x => x.StudyLabMonthlyFee)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<Enrollment>()
            .Property(x => x.StoppedAmountWaived)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<Enrollment>()
            .HasOne(e => e.Program)
            .WithMany()
            .HasForeignKey(e => e.ProgramId)
            .OnDelete(DeleteBehavior.Restrict);

    }

}
