using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanguageSchoolERP.Core.Models;
using LanguageSchoolERP.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace LanguageSchoolERP.App.ViewModels;

public record AddProgramEnrollmentInit(Guid StudentId, string AcademicYear);

public partial class AddProgramEnrollmentViewModel : ObservableObject
{
    private readonly DbContextFactory _dbFactory;

    private Guid _studentId;
    private string _academicYear = "";

    public event EventHandler<bool>? RequestClose;

    public IReadOnlyList<ProgramType> ProgramTypes { get; } =
        new[] { ProgramType.LanguageSchool, ProgramType.StudyLab, ProgramType.EuroLab };

    [ObservableProperty] private ProgramType selectedProgramType = ProgramType.LanguageSchool;
    [ObservableProperty] private string levelOrClass = "";
    [ObservableProperty] private string agreementTotalText = "0";
    [ObservableProperty] private string booksAmountText = "0";
    [ObservableProperty] private string downPaymentText = "0";
    [ObservableProperty] private string enrollmentComments = "";
    [ObservableProperty] private string installmentCountText = "0";
    [ObservableProperty] private DateTime? installmentStartMonth;
    [ObservableProperty] private string errorMessage = "";

    public IAsyncRelayCommand SaveCommand { get; }

    public AddProgramEnrollmentViewModel(DbContextFactory dbFactory)
    {
        _dbFactory = dbFactory;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }

    public void Initialize(AddProgramEnrollmentInit init)
    {
        _studentId = init.StudentId;
        _academicYear = init.AcademicYear;

        SelectedProgramType = ProgramType.LanguageSchool;
        LevelOrClass = "";
        AgreementTotalText = "0";
        BooksAmountText = "0";
        DownPaymentText = "0";
        EnrollmentComments = "";
        InstallmentCountText = "0";
        InstallmentStartMonth = null;
        ErrorMessage = "";
    }

    private async Task SaveAsync()
    {
        ErrorMessage = "";

        if (!TryParseMoney(AgreementTotalText, out var agreementTotal) || agreementTotal < 0)
        {
            ErrorMessage = "Agreement total must be a valid number (>= 0).";
            return;
        }
        if (!TryParseMoney(BooksAmountText, out var books) || books < 0)
        {
            ErrorMessage = "Books amount must be a valid number (>= 0).";
            return;
        }
        if (!TryParseMoney(DownPaymentText, out var down) || down < 0)
        {
            ErrorMessage = "Downpayment must be a valid number (>= 0).";
            return;
        }
        if (!int.TryParse(InstallmentCountText.Trim(), out var installmentCount) || installmentCount < 0 || installmentCount > 12)
        {
            ErrorMessage = "Installments count must be a number between 0 and 12.";
            return;
        }

        DateTime? startMonth = null;
        if (installmentCount > 0)
        {
            if (InstallmentStartMonth is null)
            {
                ErrorMessage = "Please select the installment start month.";
                return;
            }

            var d = InstallmentStartMonth.Value;
            startMonth = new DateTime(d.Year, d.Month, 1);
        }

        try
        {
            using var db = _dbFactory.Create();
            DbSeeder.EnsureSeeded(db);

            var period = await db.AcademicPeriods
                .FirstOrDefaultAsync(p => p.Name == _academicYear);

            if (period is null)
            {
                ErrorMessage = $"Academic year '{_academicYear}' not found.";
                return;
            }

            var studentExists = await db.Students.AnyAsync(s => s.StudentId == _studentId);
            if (!studentExists)
            {
                ErrorMessage = "Student not found.";
                return;
            }

            var enrollment = new Enrollment
            {
                StudentId = _studentId,
                AcademicPeriodId = period.AcademicPeriodId,
                ProgramType = SelectedProgramType,
                LevelOrClass = LevelOrClass.Trim(),
                AgreementTotal = agreementTotal,
                BooksAmount = books,
                DownPayment = down,
                Comments = EnrollmentComments.Trim(),
                Status = "Active",
                InstallmentCount = installmentCount,
                InstallmentStartMonth = startMonth
            };

            db.Enrollments.Add(enrollment);
            await db.SaveChangesAsync();

            RequestClose?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private static bool TryParseMoney(string text, out decimal value)
    {
        text = (text ?? "").Trim();

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
            return true;

        if (decimal.TryParse(text, NumberStyles.Number, new CultureInfo("el-GR"), out value))
            return true;

        value = 0;
        return false;
    }
}
