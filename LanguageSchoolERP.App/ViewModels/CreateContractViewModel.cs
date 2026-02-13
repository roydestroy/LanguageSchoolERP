using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanguageSchoolERP.Core.Models;
using LanguageSchoolERP.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LanguageSchoolERP.App.ViewModels;

public record ContractTemplateOption(Guid ContractTemplateId, string Name, string BranchKey)
{
    public override string ToString() => $"{Name} ({BranchKey})";
}

public record CreateContractInit(Guid StudentId, string AcademicYear);

public partial class CreateContractViewModel : ObservableObject
{
    private readonly DbContextFactory _dbFactory;

    private CreateContractInit? _init;

    public event EventHandler<bool>? RequestClose;

    public List<EnrollmentOption> EnrollmentOptions { get; } = new();
    public List<ContractTemplateOption> TemplateOptions { get; } = new();

    [ObservableProperty] private EnrollmentOption? selectedEnrollmentOption;
    [ObservableProperty] private ContractTemplateOption? selectedTemplateOption;

    [ObservableProperty] private DateTime? createdAt = DateTime.Today;

    [ObservableProperty] private string guardianFullName = "";
    [ObservableProperty] private string tuitionAmount = "";
    [ObservableProperty] private DateTime? contractStartDate = DateTime.Today;
    [ObservableProperty] private DateTime? contractEndDate = DateTime.Today.AddMonths(9);
    [ObservableProperty] private string notes = "";

    [ObservableProperty] private string errorMessage = "";

    public IRelayCommand SaveCommand { get; }

    public CreateContractViewModel(DbContextFactory dbFactory)
    {
        _dbFactory = dbFactory;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }

    public async void Initialize(CreateContractInit init)
    {
        _init = init;
        ErrorMessage = "";
        EnrollmentOptions.Clear();
        TemplateOptions.Clear();

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

        var enrollments = await db.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == init.StudentId && e.AcademicPeriodId == period.AcademicPeriodId)
            .OrderBy(e => e.ProgramType)
            .ToListAsync();

        foreach (var e in enrollments)
        {
            var label = string.IsNullOrWhiteSpace(e.LevelOrClass)
                ? $"{e.ProgramType}"
                : $"{e.ProgramType} ({e.LevelOrClass})";

            EnrollmentOptions.Add(new EnrollmentOption(e.EnrollmentId, label, e.AgreementTotal));
        }

        var templates = await db.ContractTemplates
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.BranchKey)
            .ThenBy(t => t.Name)
            .ToListAsync();

        foreach (var t in templates)
        {
            TemplateOptions.Add(new ContractTemplateOption(t.ContractTemplateId, t.Name, t.BranchKey));
        }

        SelectedEnrollmentOption = EnrollmentOptions.FirstOrDefault();
        SelectedTemplateOption = TemplateOptions.FirstOrDefault();
    }

    private async Task SaveAsync()
    {
        ErrorMessage = "";

        if (_init is null)
        {
            ErrorMessage = "Dialog not initialized.";
            return;
        }

        if (SelectedEnrollmentOption is null)
        {
            ErrorMessage = "Please select an enrollment.";
            return;
        }

        if (SelectedTemplateOption is null)
        {
            ErrorMessage = "Please select a template.";
            return;
        }

        if (CreatedAt is null)
        {
            ErrorMessage = "Please choose the created date.";
            return;
        }

        try
        {
            using var db = _dbFactory.Create();
            DbSeeder.EnsureSeeded(db);

            var data = new Dictionary<string, string?>
            {
                ["GuardianFullName"] = GuardianFullName.Trim(),
                ["TuitionAmount"] = TuitionAmount.Trim(),
                ["ContractStartDate"] = ContractStartDate?.ToString("yyyy-MM-dd"),
                ["ContractEndDate"] = ContractEndDate?.ToString("yyyy-MM-dd"),
                ["Notes"] = Notes.Trim()
            };

            var contract = new Contract
            {
                StudentId = _init.StudentId,
                EnrollmentId = SelectedEnrollmentOption.EnrollmentId,
                TemplateId = SelectedTemplateOption.ContractTemplateId,
                CreatedAt = CreatedAt.Value,
                PdfPath = null,
                DataJson = JsonSerializer.Serialize(data)
            };

            db.Contracts.Add(contract);
            await db.SaveChangesAsync();

            RequestClose?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
