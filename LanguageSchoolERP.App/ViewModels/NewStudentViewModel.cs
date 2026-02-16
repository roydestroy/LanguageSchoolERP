using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanguageSchoolERP.Core.Models;
using LanguageSchoolERP.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace LanguageSchoolERP.App.ViewModels;

public partial class NewStudentViewModel : ObservableObject
{
    private readonly AppState _state;
    private readonly DbContextFactory _dbFactory;

    public event EventHandler<bool>? RequestClose;

    public IReadOnlyList<ProgramOptionVm> ProgramTypes { get; } =
        new[]
        {
            new ProgramOptionVm(ProgramType.LanguageSchool, ProgramType.LanguageSchool.ToDisplayName()),
            new ProgramOptionVm(ProgramType.StudyLab, ProgramType.StudyLab.ToDisplayName()),
            new ProgramOptionVm(ProgramType.EuroLab, ProgramType.EuroLab.ToDisplayName())
        };

    public ProgramOptionVm? SelectedProgramOption
    {
        get => ProgramTypes.FirstOrDefault(x => x.Value == SelectedProgramType);
        set
        {
            if (value is null) return;
            SelectedProgramType = value.Value;
            OnPropertyChanged();
        }
    }

    [ObservableProperty] private ProgramType selectedProgramType = ProgramType.LanguageSchool;

    // Student
    [ObservableProperty] private string studentName = "";
    [ObservableProperty] private string studentSurname = "";
    [ObservableProperty] private DateTime? dateOfBirth;
    [ObservableProperty] private string phone = "";
    [ObservableProperty] private string email = "";
    [ObservableProperty] private string notes = "";

    // Parents
    [ObservableProperty] private string fatherName = "";
    [ObservableProperty] private string fatherSurname = "";
    [ObservableProperty] private string fatherPhone = "";
    [ObservableProperty] private string fatherEmail = "";

    [ObservableProperty] private string motherName = "";
    [ObservableProperty] private string motherSurname = "";
    [ObservableProperty] private string motherPhone = "";
    [ObservableProperty] private string motherEmail = "";

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

    [ObservableProperty] private bool includesStudyLab;
    [ObservableProperty] private string studyLabMonthlyPriceText = "";

    [ObservableProperty] private bool includesTransportation;
    [ObservableProperty] private string transportationMonthlyPriceText = "";

    public bool IsLanguageSchoolProgram => SelectedProgramType == ProgramType.LanguageSchool;
    public bool IsStudyLabProgram => SelectedProgramType == ProgramType.StudyLab;

    public IRelayCommand CreateCommand { get; }

    partial void OnSelectedProgramTypeChanged(ProgramType value)
    {
        OnPropertyChanged(nameof(SelectedProgramOption));
        OnPropertyChanged(nameof(IsLanguageSchoolProgram));
        OnPropertyChanged(nameof(IsStudyLabProgram));

        if (value != ProgramType.LanguageSchool)
        {
            IncludesStudyLab = false;
            StudyLabMonthlyPriceText = "";
        }

        if (value != ProgramType.StudyLab)
        {
            IncludesTransportation = false;
            TransportationMonthlyPriceText = "";
        }
    }

    public NewStudentViewModel(AppState state, DbContextFactory dbFactory)
    {
        _state = state;
        _dbFactory = dbFactory;
        CreateCommand = new AsyncRelayCommand(CreateAsync);
    }

    private async Task CreateAsync()
    {
        ErrorMessage = "";

        var fullName = JoinName(StudentName, StudentSurname);
        if (string.IsNullOrWhiteSpace(fullName))
        {
            ErrorMessage = "Student name is required.";
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

        decimal? studyLabPrice = null;
        if (IsLanguageSchoolProgram && IncludesStudyLab)
        {
            if (!TryParseMoney(StudyLabMonthlyPriceText, out var parsedStudyLabPrice) || parsedStudyLabPrice < 0)
            {
                ErrorMessage = "Study Lab monthly price must be a valid number (>= 0).";
                return;
            }

            studyLabPrice = parsedStudyLabPrice;
        }

        decimal? transportationPrice = null;
        if (IsStudyLabProgram && IncludesTransportation)
        {
            if (!TryParseMoney(TransportationMonthlyPriceText, out var parsedTransportationPrice) || parsedTransportationPrice < 0)
            {
                ErrorMessage = "Transportation monthly price must be a valid number (>= 0).";
                return;
            }

            transportationPrice = parsedTransportationPrice;
        }

        DateTime? startMonth = null;
        if (installmentCount > 0)
        {
            if (installmentStartMonth is null)
            {
                ErrorMessage = "Please select the installment start month.";
                return;
            }

            var d = installmentStartMonth.Value;
            startMonth = new DateTime(d.Year, d.Month, 1);
        }

        try
        {
            using var db = _dbFactory.Create();

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
                FullName = fullName,
                DateOfBirth = DateOfBirth,
                Phone = Phone.Trim(),
                Email = Email.Trim(),
                Notes = Notes.Trim(),
                FatherName = JoinName(FatherName, FatherSurname),
                FatherContact = JoinPhoneEmail(FatherPhone, FatherEmail),
                MotherName = JoinName(MotherName, MotherSurname),
                MotherContact = JoinPhoneEmail(MotherPhone, MotherEmail),
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
                InstallmentStartMonth = startMonth,
                IncludesStudyLab = IsLanguageSchoolProgram && IncludesStudyLab,
                StudyLabMonthlyPrice = IsLanguageSchoolProgram && IncludesStudyLab ? studyLabPrice : null,
                IncludesTransportation = IsStudyLabProgram && IncludesTransportation,
                TransportationMonthlyPrice = IsStudyLabProgram && IncludesTransportation ? transportationPrice : null
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

    private static string JoinName(string? name, string? surname)
    {
        return string.Join(" ", new[] { name?.Trim(), surname?.Trim() }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string JoinPhoneEmail(string? phone, string? email)
    {
        var p = phone?.Trim() ?? "";
        var e = email?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(p)) return e;
        if (string.IsNullOrWhiteSpace(e)) return p;
        return $"{p} | {e}";
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
