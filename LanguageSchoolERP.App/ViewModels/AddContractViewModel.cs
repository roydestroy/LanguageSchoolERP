using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanguageSchoolERP.Core.Models;
using LanguageSchoolERP.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.IO;

namespace LanguageSchoolERP.App.ViewModels;

public record AddContractInit(Guid StudentId, string AcademicYear, string BranchKey);

public partial class AddContractViewModel : ObservableObject
{
    private readonly DbContextFactory _dbFactory;
    private readonly ContractDocumentService _contractDocumentService;
    private readonly ContractBookmarkBuilder _bookmarkBuilder;

    private AddContractInit? _init;
    private ContractTemplate? _defaultTemplate;

    public event EventHandler<bool>? RequestClose;

    public List<EnrollmentOption> EnrollmentOptions { get; } = new();

    [ObservableProperty] private EnrollmentOption? selectedEnrollment;

    [ObservableProperty] private DateTime createdAt = DateTime.Now;
    [ObservableProperty] private string studentName = "";
    [ObservableProperty] private string guardianName = "";
    [ObservableProperty] private string notes = "";

    [ObservableProperty] private string errorMessage = "";

    public IRelayCommand SaveCommand { get; }

    public AddContractViewModel(
        DbContextFactory dbFactory,
        ContractDocumentService contractDocumentService,
        ContractBookmarkBuilder bookmarkBuilder)
    {
        _dbFactory = dbFactory;
        _contractDocumentService = contractDocumentService;
        _bookmarkBuilder = bookmarkBuilder;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }

    public async void Initialize(AddContractInit init)
    {
        _init = init;
        ErrorMessage = "";
        Notes = "";
        CreatedAt = DateTime.Now;

        EnrollmentOptions.Clear();

        using var db = _dbFactory.Create();
        DbSeeder.EnsureSeeded(db);

        var period = await db.AcademicPeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Name == init.AcademicYear);

        if (period is null)
        {
            ErrorMessage = $"Academic year '{init.AcademicYear}' not found.";
            return;
        }

        var student = await db.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.StudentId == init.StudentId);

        if (student is null)
        {
            ErrorMessage = "Student not found.";
            return;
        }

        var (_, studentSurname) = SplitName(student.FullName);
        StudentName = EnsureSurname(student.FullName, studentSurname);

        var defaultGuardian = string.IsNullOrWhiteSpace(student.FatherName) ? student.MotherName : student.FatherName;
        GuardianName = EnsureSurname(defaultGuardian, studentSurname);

        _defaultTemplate = await db.ContractTemplates
            .AsNoTracking()
            .Where(t => t.IsActive && t.BranchKey == init.BranchKey)
            .OrderBy(t => t.Name)
            .FirstOrDefaultAsync();

        if (_defaultTemplate is null)
        {
            ErrorMessage = $"No active contract template found for branch '{init.BranchKey}'.";
            return;
        }

        var enrollments = await db.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == init.StudentId && e.AcademicPeriodId == period.AcademicPeriodId)
            .OrderBy(e => e.ProgramType)
            .ThenBy(e => e.LevelOrClass)
            .ToListAsync();

        foreach (var e in enrollments)
        {
            var label = string.IsNullOrWhiteSpace(e.LevelOrClass)
                ? e.ProgramType.ToDisplayName()
                : $"{e.ProgramType.ToDisplayName()} ({e.LevelOrClass})";

            EnrollmentOptions.Add(new EnrollmentOption(e.EnrollmentId, label, 0));
        }

        SelectedEnrollment = EnrollmentOptions.FirstOrDefault();
    }

    private async Task SaveAsync()
    {
        ErrorMessage = "";

        if (_init is null)
        {
            ErrorMessage = "Dialog not initialized.";
            return;
        }

        if (SelectedEnrollment is null)
        {
            ErrorMessage = "Please select an enrollment.";
            return;
        }

        if (_defaultTemplate is null)
        {
            ErrorMessage = "No active contract template is configured for this branch.";
            return;
        }

        try
        {
            using var db = _dbFactory.Create();
            DbSeeder.EnsureSeeded(db);

            var student = await db.Students.AsNoTracking().FirstAsync(x => x.StudentId == _init.StudentId);
            var enrollment = await db.Enrollments.AsNoTracking().FirstAsync(x => x.EnrollmentId == SelectedEnrollment.EnrollmentId);
            var template = _defaultTemplate;

            var (_, studentSurname) = SplitName(student.FullName);
            var effectiveStudentName = EnsureSurname((StudentName ?? "").Trim(), studentSurname);
            var effectiveGuardianName = EnsureSurname((GuardianName ?? "").Trim(), studentSurname);

            var (firstName, lastName) = SplitName(effectiveStudentName);
            var contractId = Guid.NewGuid();

            var payload = new ContractPayload
            {
                ContractId = contractId,
                StudentId = student.StudentId,
                EnrollmentId = enrollment.EnrollmentId,
                AcademicYear = _init.AcademicYear,
                BranchKey = _init.BranchKey,
                StudentFullName = effectiveStudentName,
                StudentFirstName = firstName,
                StudentLastName = lastName,
                GuardianFullName = effectiveGuardianName,
                ProgramNameUpper = enrollment.ProgramType.ToDisplayName().ToUpperInvariant(),
                ProgramTitleUpperWithExtras = ContractBookmarkBuilder.BuildProgramTitleUpperWithExtras(enrollment),
                AgreementTotal = enrollment.AgreementTotal,
                DownPayment = enrollment.DownPayment,
                IncludesTransportation = enrollment.IncludesTransportation,
                TransportationMonthlyPrice = enrollment.TransportationMonthlyPrice,
                IncludesStudyLab = enrollment.IncludesStudyLab,
                StudyLabMonthlyPrice = enrollment.StudyLabMonthlyPrice,
                InstallmentCount = enrollment.InstallmentCount,
                InstallmentStartMonth = enrollment.InstallmentStartMonth,
                InstallmentDayOfMonth = enrollment.InstallmentDayOfMonth,
                CreatedAt = CreatedAt
            };

            var folder = ContractPathService.GetContractFolder(_init.AcademicYear, payload.StudentLastName, payload.StudentFirstName);
            var docxPath = ContractPathService.GetContractDocxPath(folder, payload.ProgramTitleUpperWithExtras, contractId);
            var templatePath = Path.Combine(AppContext.BaseDirectory, template.TemplateRelativePath);
            var financedPositive = (payload.AgreementTotal - payload.DownPayment) > 0;

            var bookmarkValues = _bookmarkBuilder.BuildBookmarkValues(payload, enrollment);
            _contractDocumentService.GenerateDocxFromTemplate(
                templatePath,
                docxPath,
                bookmarkValues,
                payload.InstallmentCount,
                financedPositive);

            var contract = new Contract
            {
                ContractId = contractId,
                StudentId = _init.StudentId,
                EnrollmentId = SelectedEnrollment.EnrollmentId,
                ContractTemplateId = template.ContractTemplateId,
                CreatedAt = CreatedAt,
                DocxPath = docxPath,
                PdfPath = null,
                DataJson = JsonSerializer.Serialize(payload)
            };

            db.Contracts.Add(contract);
            await db.SaveChangesAsync();

            RequestClose?.Invoke(this, true);
        }
        catch (DbUpdateException ex)
        {
            var inner = ex.InnerException?.Message ?? "(no inner exception)";
            ErrorMessage = $"DbUpdateException: {ex.Message}\nInner: {inner}";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.ToString();
        }

    }


    private static string EnsureSurname(string fullName, string defaultSurname)
    {
        var normalized = (fullName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return normalized;

        var (_, surname) = SplitName(normalized);
        if (!string.IsNullOrWhiteSpace(surname))
            return normalized;

        var fallbackSurname = (defaultSurname ?? "").Trim();
        if (string.IsNullOrWhiteSpace(fallbackSurname))
            return normalized;

        return $"{normalized} {fallbackSurname}".Trim();
    }

    private static (string FirstName, string LastName) SplitName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return ("", "");
        if (parts.Length == 1) return (parts[0], "");
        return (parts[0], string.Join(" ", parts.Skip(1)));
    }
}
