using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanguageSchoolERP.Core.Models;
using LanguageSchoolERP.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using static Azure.Core.HttpHeader;

namespace LanguageSchoolERP.App.ViewModels;

public partial class NewStudentViewModel : ObservableObject
{
    private readonly AppState _state;
    private readonly DbContextFactory _dbFactory;

    public event EventHandler<bool>? RequestClose;

    public IReadOnlyList<ProgramType> ProgramTypes { get; } =
        new[] { ProgramType.LanguageSchool, ProgramType.StudyLab, ProgramType.EuroLab };

    [ObservableProperty] private ProgramType selectedProgramType = ProgramType.LanguageSchool;

    // Student
    [ObservableProperty] private string fullName = "";
    [ObservableProperty] private DateTime? dateOfBirth;
    [ObservableProperty] private string phone = "";
    [ObservableProperty] private string email = "";
    [ObservableProperty] private string notes = "";

    // Parents
    [ObservableProperty] private string fatherName = "";
    [ObservableProperty] private string fatherContact = "";
    [ObservableProperty] private string motherName = "";
    [ObservableProperty] private string motherContact = "";

    // Enrollment
    [ObservableProperty] private string levelOrClass = "";
    [ObservableProperty] private string agreementTotalText = "0";
    [ObservableProperty] private string booksAmountText = "0";
    [ObservableProperty] private string downPaymentText = "0";
    [ObservableProperty] private string enrollmentComments = "";
    [ObservableProperty] private string errorMessage = "";

    // Installment plan
    [ObservableProperty] private string installmentCountText = "0";
    [ObservableProperty] private DateTime? installmentStartMonth;

    public IRelayCommand CreateCommand { get; }

    public NewStudentViewModel(AppState state, DbContextFactory dbFactory)
    {
        _state = state;
        _dbFactory = dbFactory;
        CreateCommand = new AsyncRelayCommand(CreateAsync);
    }

    private async Task CreateAsync()
    {
        ErrorMessage = "";

        if (string.IsNullOrWhiteSpace(FullName))
        {
            ErrorMessage = "Full name is required.";
            return;
        }

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
        if (!int.TryParse(installmentCountText.Trim(), out var installmentCount) || installmentCount < 0 || installmentCount > 12)
        {
            ErrorMessage = "Installments count must be a number between 0 and 12.";
            return;
        }

        DateTime? startMonth = null;
        if (installmentCount > 0)
        {
            if (installmentStartMonth is null)
            {
                ErrorMessage = "Please select the installment start month.";
                return;
            }

            // Normalize to first day of that month
            var d = installmentStartMonth.Value;
            startMonth = new DateTime(d.Year, d.Month, 1);
        }

        try
        {
            using var db = _dbFactory.Create();

            // Ensure academic periods exist
            DbSeeder.EnsureSeeded(db);

            var period = await db.AcademicPeriods
                .FirstOrDefaultAsync(p => p.Name == _state.SelectedAcademicYear);

            if (period is null)
            {
                ErrorMessage = $"Academic year '{_state.SelectedAcademicYear}' not found.";
                return;
            }

            var student = new Student
            {
                FullName = FullName.Trim(),
                DateOfBirth = DateOfBirth,
                Phone = Phone.Trim(),
                Email = Email.Trim(),
                Notes = Notes.Trim(),
                FatherName = FatherName.Trim(),
                FatherContact = FatherContact.Trim(),
                MotherName = MotherName.Trim(),
                MotherContact = MotherContact.Trim(),
            };

            var enrollment = new Enrollment
            {
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

            student.Enrollments.Add(enrollment);

            db.Students.Add(student);
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

        // Accept "123", "123.45", "123,45"
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
            return true;

        if (decimal.TryParse(text, NumberStyles.Number, new CultureInfo("el-GR"), out value))
            return true;

        value = 0;
        return false;
    }
}
