using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanguageSchoolERP.Core.Models;
using LanguageSchoolERP.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace LanguageSchoolERP.App.ViewModels;

public record AddContractInit(Guid StudentId, string AcademicYear, string BranchKey);

public record ContractTemplateOption(Guid ContractTemplateId, string Name)
{
    public override string ToString() => Name;
}

public partial class AddContractViewModel : ObservableObject
{
    private readonly DbContextFactory _dbFactory;

    private AddContractInit? _init;

    public event EventHandler<bool>? RequestClose;

    public List<ContractTemplateOption> TemplateOptions { get; } = new();
    public List<EnrollmentOption> EnrollmentOptions { get; } = new();

    [ObservableProperty] private ContractTemplateOption? selectedTemplate;
    [ObservableProperty] private EnrollmentOption? selectedEnrollment;

    [ObservableProperty] private DateTime createdAt = DateTime.Now;
    [ObservableProperty] private string studentName = "";
    [ObservableProperty] private string guardianName = "";
    [ObservableProperty] private string notes = "";

    [ObservableProperty] private string errorMessage = "";

    public IRelayCommand SaveCommand { get; }

    public AddContractViewModel(DbContextFactory dbFactory)
    {
        _dbFactory = dbFactory;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }

    public async void Initialize(AddContractInit init)
    {
        _init = init;
        ErrorMessage = "";
        Notes = "";
        CreatedAt = DateTime.Now;

        TemplateOptions.Clear();
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

        StudentName = student.FullName;
        GuardianName = string.IsNullOrWhiteSpace(student.FatherName) ? student.MotherName : student.FatherName;

        var templates = await db.ContractTemplates
            .AsNoTracking()
            .Where(t => t.IsActive && t.BranchKey == init.BranchKey)
            .OrderBy(t => t.Name)
            .ToListAsync();

        foreach (var t in templates)
        {
            TemplateOptions.Add(new ContractTemplateOption(t.ContractTemplateId, t.Name));
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
                ? e.ProgramType.ToString()
                : $"{e.ProgramType} ({e.LevelOrClass})";

            EnrollmentOptions.Add(new EnrollmentOption(e.EnrollmentId, label, 0));
        }

        SelectedTemplate = TemplateOptions.FirstOrDefault();
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

        if (SelectedTemplate is null)
        {
            ErrorMessage = "Please select a template.";
            return;
        }

        if (SelectedEnrollment is null)
        {
            ErrorMessage = "Please select an enrollment.";
            return;
        }

        try
        {
            using var db = _dbFactory.Create();
            DbSeeder.EnsureSeeded(db);

            var payload = new
            {
                StudentName = StudentName?.Trim() ?? "",
                GuardianName = GuardianName?.Trim() ?? "",
                Notes = Notes?.Trim() ?? "",
                AcademicYear = _init.AcademicYear,
                BranchKey = _init.BranchKey
            };

            var contract = new Contract
            {
                StudentId = _init.StudentId,
                EnrollmentId = SelectedEnrollment.EnrollmentId,
                ContractTemplateId = SelectedTemplate.ContractTemplateId,
                CreatedAt = CreatedAt,
                PdfPath = null,
                DataJson = JsonSerializer.Serialize(payload)
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
