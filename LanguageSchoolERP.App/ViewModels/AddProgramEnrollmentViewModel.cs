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

public record AddProgramEnrollmentInit(Guid StudentId, string AcademicYear, Guid? EnrollmentId = null);

public partial class AddProgramEnrollmentViewModel : ObservableObject
{
    private readonly DbContextFactory _dbFactory;
    private readonly AppState _state;

    private Guid _studentId;
    private string _academicYear = "";
    private Guid? _editingEnrollmentId;

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
            if (value is null)
            {
                return;
            }

            SelectedProgramType = value.Value;
            OnPropertyChanged();
        }
    }

    [ObservableProperty] private ProgramType selectedProgramType = ProgramType.LanguageSchool;
    [ObservableProperty] private string levelOrClass = "";
    [ObservableProperty] private string agreementTotalText = "0";
    [ObservableProperty] private string booksAmountText = "0";
    [ObservableProperty] private string downPaymentText = "0";
    [ObservableProperty] private string enrollmentComments = "";
    [ObservableProperty] private string installmentCountText = "0";
    [ObservableProperty] private DateTime? installmentStartMonth;

    [ObservableProperty] private bool includesStudyLab;
    [ObservableProperty] private string studyLabMonthlyPriceText = "";

    [ObservableProperty] private bool includesTransportation;
    [ObservableProperty] private string transportationMonthlyPriceText = "";

    public bool IsLanguageSchoolProgram => SelectedProgramType == ProgramType.LanguageSchool;
    public bool IsStudyLabProgram => SelectedProgramType == ProgramType.StudyLab;

    [ObservableProperty] private string errorMessage = "";
    [ObservableProperty] private string dialogTitle = "Προσθήκη εγγραφής προγράμματος";
    [ObservableProperty] private string saveButtonText = "Προσθήκη";

    public IAsyncRelayCommand SaveCommand { get; }

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

    public AddProgramEnrollmentViewModel(DbContextFactory dbFactory, AppState state)
    {
        _dbFactory = dbFactory;
        _state = state;
        SaveCommand = new AsyncRelayCommand(SaveAsync, CanWrite);

        _state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.SelectedDatabaseMode))
                SaveCommand.NotifyCanExecuteChanged();
        };
    }

    private bool CanWrite() => !_state.IsReadOnlyMode;

    public async void Initialize(AddProgramEnrollmentInit init)
    {
        _studentId = init.StudentId;
        _academicYear = init.AcademicYear;
        _editingEnrollmentId = init.EnrollmentId;

        SelectedProgramType = ProgramType.LanguageSchool;
        LevelOrClass = "";
        AgreementTotalText = "0";
        BooksAmountText = "0";
        DownPaymentText = "0";
        EnrollmentComments = "";
        InstallmentCountText = "0";
        InstallmentStartMonth = null;
        IncludesStudyLab = false;
        StudyLabMonthlyPriceText = "";
        IncludesTransportation = false;
        TransportationMonthlyPriceText = "";
        ErrorMessage = "";

        if (_editingEnrollmentId.HasValue)
        {
            DialogTitle = "Επεξεργασία εγγραφής προγράμματος";
            SaveButtonText = "Αποθήκευση αλλαγών";

            try
            {
                using var db = _dbFactory.Create();
                DbSeeder.EnsureSeeded(db);

                var enrollment = await db.Enrollments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.EnrollmentId == _editingEnrollmentId.Value && e.StudentId == _studentId);

                if (enrollment is null)
                {
                    ErrorMessage = "Η εγγραφή δεν βρέθηκε.";
                    return;
                }

                SelectedProgramType = enrollment.ProgramType;
                LevelOrClass = enrollment.LevelOrClass ?? "";
                AgreementTotalText = enrollment.AgreementTotal.ToString("0.00", CultureInfo.InvariantCulture);
                BooksAmountText = enrollment.BooksAmount.ToString("0.00", CultureInfo.InvariantCulture);
                DownPaymentText = enrollment.DownPayment.ToString("0.00", CultureInfo.InvariantCulture);
                EnrollmentComments = enrollment.Comments ?? "";
                InstallmentCountText = enrollment.InstallmentCount.ToString(CultureInfo.InvariantCulture);
                InstallmentStartMonth = enrollment.InstallmentStartMonth;
                IncludesStudyLab = enrollment.IncludesStudyLab;
                StudyLabMonthlyPriceText = enrollment.StudyLabMonthlyPrice?.ToString("0.00", CultureInfo.InvariantCulture) ?? "";
                IncludesTransportation = enrollment.IncludesTransportation;
                TransportationMonthlyPriceText = enrollment.TransportationMonthlyPrice?.ToString("0.00", CultureInfo.InvariantCulture) ?? "";
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }
        else
        {
            DialogTitle = "Προσθήκη εγγραφής προγράμματος";
            SaveButtonText = "Προσθήκη";
        }
    }

    private async Task SaveAsync()
    {
        if (!CanWrite())
        {
            ErrorMessage = "Η απομακρυσμένη λειτουργία είναι μόνο για ανάγνωση.";
            return;
        }

        ErrorMessage = "";

        if (!TryParseMoney(AgreementTotalText, out var agreementTotal) || agreementTotal < 0)
        {
            ErrorMessage = "Το σύνολο συμφωνίας πρέπει να είναι έγκυρος αριθμός (>= 0).";
            return;
        }
        if (!TryParseMoney(BooksAmountText, out var books) || books < 0)
        {
            ErrorMessage = "Το ποσό βιβλίων πρέπει να είναι έγκυρος αριθμός (>= 0).";
            return;
        }
        if (!TryParseMoney(DownPaymentText, out var down) || down < 0)
        {
            ErrorMessage = "Η προκαταβολή πρέπει να είναι έγκυρος αριθμός (>= 0).";
            return;
        }
        if (!int.TryParse(InstallmentCountText.Trim(), out var installmentCount) || installmentCount < 0 || installmentCount > 12)
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
            if (InstallmentStartMonth is null)
            {
                ErrorMessage = "Παρακαλώ επιλέξτε μήνα έναρξης δόσεων.";
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
                ErrorMessage = $"Το ακαδημαϊκό έτος '{_academicYear}' δεν βρέθηκε.";
                return;
            }

            var studentExists = await db.Students.AnyAsync(s => s.StudentId == _studentId);
            if (!studentExists)
            {
                ErrorMessage = "Ο μαθητής δεν βρέθηκε.";
                return;
            }

            if (_editingEnrollmentId.HasValue)
            {
                var enrollment = await db.Enrollments
                    .FirstOrDefaultAsync(e => e.EnrollmentId == _editingEnrollmentId.Value && e.StudentId == _studentId);

                if (enrollment is null)
                {
                    ErrorMessage = "Η εγγραφή δεν βρέθηκε.";
                    return;
                }

                enrollment.ProgramType = SelectedProgramType;
                enrollment.LevelOrClass = LevelOrClass.Trim();
                enrollment.AgreementTotal = agreementTotal;
                enrollment.BooksAmount = books;
                enrollment.DownPayment = down;
                enrollment.Comments = EnrollmentComments.Trim();
                enrollment.InstallmentCount = installmentCount;
                enrollment.InstallmentStartMonth = startMonth;
                enrollment.IncludesStudyLab = IsLanguageSchoolProgram && IncludesStudyLab;
                enrollment.StudyLabMonthlyPrice = IsLanguageSchoolProgram && IncludesStudyLab ? studyLabPrice : null;
                enrollment.IncludesTransportation = IsStudyLabProgram && IncludesTransportation;
                enrollment.TransportationMonthlyPrice = IsStudyLabProgram && IncludesTransportation ? transportationPrice : null;
            }
            else
            {
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
                    Status = "Ενεργός",
                    InstallmentCount = installmentCount,
                    InstallmentStartMonth = startMonth,
                    IncludesStudyLab = IsLanguageSchoolProgram && IncludesStudyLab,
                    StudyLabMonthlyPrice = IsLanguageSchoolProgram && IncludesStudyLab ? studyLabPrice : null,
                    IncludesTransportation = IsStudyLabProgram && IncludesTransportation,
                    TransportationMonthlyPrice = IsStudyLabProgram && IncludesTransportation ? transportationPrice : null
                };

                db.Enrollments.Add(enrollment);
            }

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

