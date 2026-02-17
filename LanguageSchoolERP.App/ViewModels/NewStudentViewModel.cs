using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanguageSchoolERP.Core.Models;
using LanguageSchoolERP.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LanguageSchoolERP.App.ViewModels;

public partial class NewStudentViewModel : ObservableObject
{
    private readonly AppState _state;
    private readonly DbContextFactory _dbFactory;
    private readonly IProgramService _programService;

    public event EventHandler<bool>? RequestClose;

    public ObservableCollection<StudyProgram> StudyPrograms { get; } = new();

    [ObservableProperty] private StudyProgram? selectedStudyProgram;
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
    public bool HasBooksOption => SelectedProgramType == ProgramType.LanguageSchool;

    public IRelayCommand CreateCommand { get; }

    partial void OnSelectedProgramTypeChanged(ProgramType value)
    {
        OnPropertyChanged(nameof(IsLanguageSchoolProgram));
        OnPropertyChanged(nameof(IsStudyLabProgram));
        OnPropertyChanged(nameof(HasBooksOption));

        if (value != ProgramType.LanguageSchool)
        {
            IncludesStudyLab = false;
            StudyLabMonthlyPriceText = "";
            BooksAmountText = "0";
        }

        if (value != ProgramType.StudyLab)
        {
            IncludesTransportation = false;
            TransportationMonthlyPriceText = "";
        }
    }

    partial void OnSelectedStudyProgramChanged(StudyProgram? value)
    {
        if (ProgramTypeResolver.TryResolveLegacyType(value, out var mappedType, out var mappingError))
        {
            ErrorMessage = string.Empty;
            SelectedProgramType = mappedType;
            return;
        }

        if (!string.IsNullOrWhiteSpace(mappingError))
        {
            ErrorMessage = mappingError;
        }
    }

    public NewStudentViewModel(AppState state, DbContextFactory dbFactory, IProgramService programService)
    {
        _state = state;
        _dbFactory = dbFactory;
        _programService = programService;
        CreateCommand = new AsyncRelayCommand(CreateAsync, CanWrite);
        _ = LoadProgramsAsync();

        _state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.SelectedDatabaseMode))
                CreateCommand.NotifyCanExecuteChanged();
        };
    }

    private bool CanWrite() => !_state.IsReadOnlyMode;

    private async Task LoadProgramsAsync()
    {
        try
        {
            var programs = await _programService.GetAllAsync(CancellationToken.None);
            StudyPrograms.Clear();
            foreach (var program in programs)
            {
                StudyPrograms.Add(program);
            }

            SelectedStudyProgram = StudyPrograms.FirstOrDefault();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private async Task CreateAsync()
    {
        if (!CanWrite())
        {
            ErrorMessage = "Η απομακρυσμένη λειτουργία είναι μόνο για ανάγνωση.";
            return;
        }
        ErrorMessage = "";

        if (!ProgramTypeResolver.TryResolveLegacyType(SelectedStudyProgram, out var mappedProgramType, out var mappingError))
        {
            ErrorMessage = mappingError ?? "Παρακαλώ επιλέξτε πρόγραμμα.";
            return;
        }

        SelectedProgramType = mappedProgramType;

        var fullName = JoinName(StudentName, StudentSurname);
        if (string.IsNullOrWhiteSpace(fullName))
        {
            ErrorMessage = "Το όνομα μαθητή είναι υποχρεωτικό.";
            return;
        }

        if (!TryParseMoney(AgreementTotalText, out var agreementTotal) || agreementTotal < 0)
        {
            ErrorMessage = "Το σύνολο συμφωνίας πρέπει να είναι έγκυρος αριθμός (>= 0).";
            return;
        }
        decimal books = 0;
        if (HasBooksOption)
        {
            if (!TryParseMoney(BooksAmountText, out books) || books < 0)
            {
                ErrorMessage = "Το ποσό βιβλίων πρέπει να είναι έγκυρος αριθμός (>= 0).";
                return;
            }
        }
        if (!TryParseMoney(DownPaymentText, out var down) || down < 0)
        {
            ErrorMessage = "Η προκαταβολή πρέπει να είναι έγκυρος αριθμός (>= 0).";
            return;
        }
        if (!int.TryParse(installmentCountText.Trim(), out var installmentCount) || installmentCount < 0 || installmentCount > 12)
        {
            ErrorMessage = "Ο αριθμός δόσεων πρέπει να είναι μεταξύ 0 και 12.";
            return;
        }

        decimal? studyLabPrice = null;
        if (IsLanguageSchoolProgram && IncludesStudyLab)
        {
            if (!TryParseMoney(StudyLabMonthlyPriceText, out var parsedStudyLabPrice) || parsedStudyLabPrice < 0)
            {
                ErrorMessage = "Η μηνιαία τιμή αίθουσας μελέτης πρέπει να είναι έγκυρος αριθμός (>= 0).";
                return;
            }

            studyLabPrice = parsedStudyLabPrice;
        }

        decimal? transportationPrice = null;
        if (IsStudyLabProgram && IncludesTransportation)
        {
            if (!TryParseMoney(TransportationMonthlyPriceText, out var parsedTransportationPrice) || parsedTransportationPrice < 0)
            {
                ErrorMessage = "Η μηνιαία τιμή μεταφοράς πρέπει να είναι έγκυρος αριθμός (>= 0).";
                return;
            }

            transportationPrice = parsedTransportationPrice;
        }

        DateTime? startMonth = null;
        if (installmentCount > 0)
        {
            if (installmentStartMonth is null)
            {
                ErrorMessage = "Παρακαλώ επιλέξτε μήνα έναρξης δόσεων.";
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
                ErrorMessage = $"Το ακαδημαϊκό έτος '{_state.SelectedAcademicYear}' δεν βρέθηκε.";
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
                Status = "Ενεργός",
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
