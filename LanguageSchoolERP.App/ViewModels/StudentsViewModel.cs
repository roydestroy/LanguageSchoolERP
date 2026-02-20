using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanguageSchoolERP.Core.Models;
using LanguageSchoolERP.Services;
using LanguageSchoolERP.Data;
using Microsoft.EntityFrameworkCore;
using LanguageSchoolERP.App.Windows;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Media;


namespace LanguageSchoolERP.App.ViewModels;

public partial class StudentsViewModel : ObservableObject
{
    private const string AllStudentsFilter = "Όλοι οι μαθητές";
    private const string ActiveStudentsFilter = "Ενεργοί";
    private const string InactiveStudentsFilter = "Ανενεργοί";
    private const string ContractPendingFilter = "Εκκρεμεί συμφωνητικό";
    private const string OverdueFilter = "Ληξιπρόθεσμα";
    private const string DiscontinuedFilter = "Με διακοπή";

    private const string SortByName = "Όνομα (Α-Ω)";
    private const string SortByBalance = "Υπόλοιπο (φθίνουσα)";
    private const string SortByOverdueAmount = "Ληξιπρόθεσμο ποσό (φθίνουσα)";

    private static readonly Brush ProgressBlueBrush = new SolidColorBrush(Color.FromRgb(78, 153, 228));
    private static readonly Brush ProgressOrangeBrush = new SolidColorBrush(Color.FromRgb(230, 145, 56));
    private static readonly Brush ProgressGreenBrush = new SolidColorBrush(Color.FromRgb(67, 160, 71));
    private static readonly Brush ProgressPurpleBrush = new SolidColorBrush(Color.FromRgb(123, 97, 255));
    private static readonly Brush ProgressStoppedRedBrush = new SolidColorBrush(Color.FromRgb(177, 38, 38));

    private readonly AppState _state;
    private readonly DbContextFactory _dbFactory;
    private int _loadGeneration;
    private bool _suppressProgramFilterReload;
    private bool _suppressSuggestionsOpenOnce;

    public ObservableCollection<StudentRowVm> Students { get; } = new();
    public ObservableCollection<string> StudentStatusFilters { get; } =
    [
        ActiveStudentsFilter,
        InactiveStudentsFilter,
        ContractPendingFilter,
        OverdueFilter,
        DiscontinuedFilter,
        AllStudentsFilter
    ];

    public ObservableCollection<string> StudentSortOptions { get; } =
    [
        SortByName,
        SortByBalance,
        SortByOverdueAmount
    ];

    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private string selectedStudentStatusFilter = ActiveStudentsFilter;
    [ObservableProperty] private string selectedStudentSortOption = SortByName;
    [ObservableProperty] private ProgramFilterItemVm? selectedProgramFilter;
    [ObservableProperty] private bool isSearchSuggestionsOpen;

    public ObservableCollection<ProgramFilterItemVm> ProgramFilters { get; } = new();
    public ObservableCollection<string> SearchSuggestions { get; } = new();

    public IRelayCommand RefreshCommand { get; }
    public IRelayCommand NewStudentCommand { get; }
    public IRelayCommand<Guid> OpenStudentCommand { get; }
    public IRelayCommand<Guid> QuickAddPaymentCommand { get; }
    public IRelayCommand<ProgramFilterItemVm> SelectProgramFilterCommand { get; }
    public IRelayCommand<string> ApplySearchSuggestionCommand { get; }

    public StudentsViewModel(AppState state, DbContextFactory dbFactory)
    {
        _state = state;
        _dbFactory = dbFactory;

        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        NewStudentCommand = new RelayCommand(OpenNewStudentDialog, CanCreateStudent);
        OpenStudentCommand = new RelayCommand<Guid>(OpenStudent);
        QuickAddPaymentCommand = new RelayCommand<Guid>(OpenQuickPaymentDialog, CanOpenQuickPayment);
        SelectProgramFilterCommand = new RelayCommand<ProgramFilterItemVm>(SelectProgramFilter);
        ApplySearchSuggestionCommand = new RelayCommand<string>(ApplySearchSuggestion);

        var allProgramsFilter = new ProgramFilterItemVm(null, "ΟΛΑ") { IsSelected = true };
        ProgramFilters.Add(allProgramsFilter);
        selectedProgramFilter = allProgramsFilter;

        // Refresh automatically when DB/year changes
        _state.PropertyChanged += OnAppStateChanged;

        // Initial load
        _ = LoadAsync();
    }

    private void OnAppStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppState.SelectedAcademicYear) ||
            e.PropertyName == nameof(AppState.SelectedDatabaseName))
        {
            _ = LoadAsync();
        }

        if (e.PropertyName == nameof(AppState.SelectedDatabaseMode))
        {
            NewStudentCommand.NotifyCanExecuteChanged();
            QuickAddPaymentCommand.NotifyCanExecuteChanged();
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            SearchSuggestions.Clear();
            IsSearchSuggestionsOpen = false;
        }

        _ = LoadAsync();
    }

    partial void OnSelectedStudentStatusFilterChanged(string value)
    {
        _ = LoadAsync();
    }

    partial void OnSelectedStudentSortOptionChanged(string value)
    {
        _ = LoadAsync();
    }

    partial void OnSelectedProgramFilterChanged(ProgramFilterItemVm? value)
    {
        foreach (var item in ProgramFilters)
            item.IsSelected = ReferenceEquals(item, value);

        if (_suppressProgramFilterReload)
            return;

        _ = LoadAsync();
    }

    private bool CanCreateStudent() => !_state.IsReadOnlyMode;


    private bool CanOpenQuickPayment(Guid enrollmentId)
    {
        return !_state.IsReadOnlyMode && enrollmentId != Guid.Empty;
    }

    private void OpenQuickPaymentDialog(Guid enrollmentId)
    {
        if (_state.IsReadOnlyMode)
        {
            System.Windows.MessageBox.Show("Η απομακρυσμένη λειτουργία είναι μόνο για ανάγνωση.");
            return;
        }

        if (enrollmentId == Guid.Empty)
            return;

        _ = OpenQuickPaymentDialogAsync(enrollmentId);
    }

    private async Task OpenQuickPaymentDialogAsync(Guid enrollmentId)
    {
        using var db = _dbFactory.Create();
        DbSeeder.EnsureSeeded(db);

        var enrollment = await db.Enrollments
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EnrollmentId == enrollmentId);

        if (enrollment is null)
            return;

        var period = await db.AcademicPeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.AcademicPeriodId == enrollment.AcademicPeriodId);

        var yearName = period?.Name ?? _state.SelectedAcademicYear;

        var win = App.Services.GetRequiredService<AddPaymentWindow>();
        win.Owner = System.Windows.Application.Current.MainWindow;
        win.Initialize(new AddPaymentInit(enrollment.StudentId, yearName, PaymentId: null, EnrollmentId: enrollment.EnrollmentId, IsQuickPrintFlow: true));

        var result = win.ShowDialog();
        if (result == true)
            await LoadAsync();
    }

    private void OpenNewStudentDialog()
    {
        if (!CanCreateStudent())
        {
            System.Windows.MessageBox.Show("Η απομακρυσμένη λειτουργία είναι μόνο για ανάγνωση.");
            return;
        }
        var win = App.Services.GetRequiredService<LanguageSchoolERP.App.Windows.NewStudentWindow>();
        win.Owner = System.Windows.Application.Current.MainWindow;

        var result = win.ShowDialog();
        if (result == true)
        {
            _ = LoadAsync();
        }
    }
    private void OpenStudent(Guid studentId)
    {
        var win = App.Services.GetRequiredService<StudentProfileWindow>();
        win.Owner = System.Windows.Application.Current.MainWindow;

        // Initialize the VM for this specific student + global year default
        win.Initialize(studentId);

        win.Show();

    }

    private void SelectProgramFilter(ProgramFilterItemVm? filter)
    {
        if (filter is null)
            return;

        SelectedProgramFilter = filter;
    }

    private void ApplySearchSuggestion(string? suggestion)
    {
        if (string.IsNullOrWhiteSpace(suggestion))
            return;

        _suppressSuggestionsOpenOnce = true;
        SearchText = suggestion;
        IsSearchSuggestionsOpen = false;
    }

    private async Task LoadAsync()
    {
        var generation = Interlocked.Increment(ref _loadGeneration);

        try
        {
            using var db = _dbFactory.Create();
            System.Diagnostics.Debug.WriteLine("DB=" + db.Database.GetDbConnection().DataSource + " | " + db.Database.GetDbConnection().Database);

            DbSeeder.EnsureSeeded(db);

            var year = _state.SelectedAcademicYear;

            // Selected academic period is optional; students should remain visible even if no period exists.
            var period = await db.AcademicPeriods
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Name == year);
            var selectedPeriodId = period?.AcademicPeriodId;

            var availablePrograms = await db.Programs
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .ToListAsync();

            SyncProgramFilters(availablePrograms);
            await LoadSearchSuggestionsAsync(db, selectedPeriodId, generation);

            var baseQuery = ApplyActiveFilters(db.Students.AsNoTracking(), db, selectedPeriodId);

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var st = SearchText.Trim();
                baseQuery = baseQuery.Where(s =>
                    (s.FirstName + " " + s.LastName).Contains(st) ||
                    s.Mobile.Contains(st) ||
                    s.Landline.Contains(st) ||
                    s.Email.Contains(st) ||
                    db.Enrollments.Any(e =>
                        e.StudentId == s.StudentId &&
                        (selectedPeriodId == null || e.AcademicPeriodId == selectedPeriodId) &&
                        e.LevelOrClass != null &&
                        e.LevelOrClass.Contains(st)));
            }

            var students = await baseQuery
                 .Include(s => s.Enrollments.Where(en => selectedPeriodId == null || en.AcademicPeriodId == selectedPeriodId))
                    .ThenInclude(en => en.Program)
                .Include(s => s.Enrollments.Where(en => selectedPeriodId == null || en.AcademicPeriodId == selectedPeriodId))
                    .ThenInclude(en => en.Payments)
                .Include(s => s.Contracts.Where(c => selectedPeriodId == null || c.Enrollment.AcademicPeriodId == selectedPeriodId))
                .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
                .ToListAsync();

            var rows = new List<StudentRowVm>();

            foreach (var s in students)
            {
                // Aggregate across enrollments in selected year
                var yearEnrollments = s.Enrollments.ToList();

                var activeEnrollments = yearEnrollments.Where(en => !en.IsStopped).ToList();

                decimal activeAgreementSum = activeEnrollments.Sum(e => e.AgreementTotal);
                decimal activeDownSum = activeEnrollments.Sum(en => en.DownPayment);
                decimal activePaidSum = activeEnrollments.Sum(en => PaymentAgreementHelper.SumAgreementPayments(en.Payments));

                var totalProgress = activeAgreementSum <= 0 ? 0d : (double)((activeDownSum + activePaidSum) / activeAgreementSum * 100m);
                if (totalProgress > 100) totalProgress = 100;
                if (totalProgress < 0) totalProgress = 0;

                var today = DateTime.Today;

                // Overdue if ANY enrollment is overdue based on installment plan
                bool overdue = yearEnrollments.Any(en => InstallmentPlanHelper.IsEnrollmentOverdue(en, today));
                bool hasPendingContract = s.Contracts.Any(c => string.IsNullOrWhiteSpace(c.PdfPath));

                var overdueAmount = yearEnrollments
                    .Where(en => InstallmentPlanHelper.IsEnrollmentOverdue(en, today))
                    .Sum(en =>
                    {
                        return InstallmentPlanHelper.GetOutstandingAmount(en);
                    });

                if (SelectedStudentStatusFilter == OverdueFilter && !overdue)
                    continue;

                if (SelectedStudentStatusFilter == ContractPendingFilter && !hasPendingContract)
                    continue;

                var hasStoppedProgram = yearEnrollments.Any(en => en.IsStopped);
                var hasOnlyStoppedPrograms = yearEnrollments.Count > 0 && yearEnrollments.All(en => en.IsStopped);

                if (SelectedStudentStatusFilter == DiscontinuedFilter && !hasStoppedProgram)
                    continue;

                var activeBalance = activeEnrollments.Sum(e => InstallmentPlanHelper.GetEffectiveAgreementTotal(e) - (e.DownPayment + PaymentAgreementHelper.SumAgreementPayments(e.Payments)));
                var anyActiveOverdue = activeEnrollments.Any(en => InstallmentPlanHelper.IsEnrollmentOverdue(en, today));
                var anyActiveOverpaid = activeEnrollments.Any(en => (en.DownPayment + PaymentAgreementHelper.SumAgreementPayments(en.Payments)) > InstallmentPlanHelper.GetEffectiveAgreementTotal(en) + 0.009m);
                var allActiveFullyPaid = activeEnrollments.Count > 0 && activeEnrollments.All(en => (en.DownPayment + PaymentAgreementHelper.SumAgreementPayments(en.Payments)) + 0.009m >= InstallmentPlanHelper.GetEffectiveAgreementTotal(en));

                var studentProgressBrush = hasOnlyStoppedPrograms
                    ? ProgressStoppedRedBrush
                    : anyActiveOverpaid
                        ? ProgressPurpleBrush
                        : anyActiveOverdue
                            ? ProgressOrangeBrush
                            : allActiveFullyPaid
                                ? ProgressGreenBrush
                                : ProgressBlueBrush;

                var activeEnrollmentSummaryItems = yearEnrollments
                    .Where(en => !en.IsStopped)
                    .OrderBy(en => en.Program?.Name)
                    .Select(en =>
                    {
                        var programName = en.Program?.Name ?? "—";
                        return string.IsNullOrWhiteSpace(en.LevelOrClass)
                            ? programName
                            : $"{programName} ({en.LevelOrClass})";
                    })
                    .ToList();

                var enrollmentSummaryText = activeEnrollmentSummaryItems.Count == 0
                    ? "Προγράμματα: —"
                    : $"Προγράμματα: {string.Join(" · ", activeEnrollmentSummaryItems)}";

                var row = new StudentRowVm
                {
                    StudentId = s.StudentId,
                    FullName = ToSurnameFirst($"{s.FirstName} {s.LastName}"),
                    ContactLine = BuildPreferredContactLine(s),
                    YearLabel = $"Έτος: {year}",
                    EnrollmentSummaryText = enrollmentSummaryText,
                    Balance = activeBalance,
                    OverdueAmount = overdueAmount,
                    ProgressPercent = totalProgress,
                    ProgressText = $"{totalProgress:0}%",
                    ProgressBrush = studentProgressBrush,
                    IsOverdue = overdue,
                    HasStoppedProgram = hasStoppedProgram,
                    HasOnlyStoppedPrograms = hasOnlyStoppedPrograms,
                    HasPendingContract = hasPendingContract,
                    IsActive = activeEnrollments.Count > 0,
                    IsExpanded = false
                };

                foreach (var en in yearEnrollments.OrderBy(x => x.Program?.Name))
                {
                    var enPaid = PaymentAgreementHelper.SumAgreementPayments(en.Payments) + en.DownPayment;
                    var enBalance = InstallmentPlanHelper.GetEffectiveAgreementTotal(en) - enPaid;
                    var enOverdue = InstallmentPlanHelper.IsEnrollmentOverdue(en, today);
                    var enOverpaid = enPaid > InstallmentPlanHelper.GetEffectiveAgreementTotal(en) + 0.009m;
                    var enFullyPaid = enPaid + 0.009m >= InstallmentPlanHelper.GetEffectiveAgreementTotal(en);

                    var enrollmentEffectiveTotal = InstallmentPlanHelper.GetEffectiveAgreementTotal(en);
                    var enrollmentProgress = enrollmentEffectiveTotal <= 0 ? 0d : (double)(enPaid / enrollmentEffectiveTotal * 100m);
                    if (enrollmentProgress > 100) enrollmentProgress = 100;
                    if (enrollmentProgress < 0) enrollmentProgress = 0;

                    var enrollmentProgressBrush = en.IsStopped
                        ? ProgressStoppedRedBrush
                        : enOverpaid
                            ? ProgressPurpleBrush
                            : enOverdue
                                ? ProgressOrangeBrush
                                : enFullyPaid
                                    ? ProgressGreenBrush
                                    : ProgressBlueBrush;

                    row.Enrollments.Add(new EnrollmentRowVm
                    {
                        EnrollmentId = en.EnrollmentId,
                        Title = en.Program?.Name ?? "—",
                        Details = string.IsNullOrWhiteSpace(en.LevelOrClass) ? "" : $"Επίπεδο/Τάξη: {en.LevelOrClass}",
                        AgreementText = $"Συμφωνία: {FormatCurrency(en.AgreementTotal)}",
                        PaidText = $"Πληρωμένα: {FormatCurrency(enPaid)}",
                        BalanceText = $"Υπόλοιπο: {FormatCurrency(enBalance)}",
                        ProgressPercent = enrollmentProgress,
                        ProgressText = $"{enrollmentProgress:0}%",
                        ProgressBrush = enrollmentProgressBrush,
                        IsStopped = en.IsStopped,
                        CanIssuePayment = !en.IsStopped
                    });
                }

                rows.Add(row);
            }

            var sortedRows = SelectedStudentSortOption switch
            {
                SortByBalance => rows.OrderByDescending(r => r.Balance).ThenBy(r => r.FullName),
                SortByOverdueAmount => rows.OrderByDescending(r => r.OverdueAmount).ThenBy(r => r.FullName),
                _ => rows.OrderBy(r => r.FullName)
            };

            // Ignore stale completion from older overlapping loads.
            if (generation != Volatile.Read(ref _loadGeneration))
                return;

            Students.Clear();
            foreach (var row in sortedRows)
                Students.Add(row);
        }
        catch (Exception ex)
        {
            // Ignore stale completion from older overlapping loads.
            if (generation != Volatile.Read(ref _loadGeneration))
                return;

            var msg = ex.InnerException?.Message ?? ex.Message;
            var normalized = msg.ToLowerInvariant();

            if (!_state.IsRemoteModeEnabled && !_state.HasAnyLocalDatabase)
            {
                msg = "Δεν υπάρχει διαθέσιμη βάση δεδομένων. Συνδεθείτε στο Tailscale ή εισάγετε τοπική βάση από τις Ρυθμίσεις.";
            }
            else if ((normalized.Contains("timeout") || normalized.Contains("timed out")) && !_state.IsRemoteModeEnabled)
            {
                msg = "Η remote βάση δεν είναι διαθέσιμη. Συνδεθείτε στο Tailscale ή εργαστείτε με τοπική βάση.";
            }

            System.Diagnostics.Debug.WriteLine(ex.ToString());
            System.Windows.MessageBox.Show(msg, "Αποτυχία φόρτωσης μαθητών");
        }

    }

    private void SyncProgramFilters(IReadOnlyCollection<StudyProgram> programs)
    {
        var selectedProgramId = SelectedProgramFilter?.ProgramId;
        var items = new List<ProgramFilterItemVm>
        {
            new(null, "ΟΛΑ")
        };

        items.AddRange(programs.Select(p => new ProgramFilterItemVm(p.Id, p.Name)));

        _suppressProgramFilterReload = true;

        ProgramFilters.Clear();
        foreach (var item in items)
            ProgramFilters.Add(item);

        SelectedProgramFilter = ProgramFilters.FirstOrDefault(f => f.ProgramId == selectedProgramId) ?? ProgramFilters.First();

        _suppressProgramFilterReload = false;
    }

    private IQueryable<Student> ApplyActiveFilters(IQueryable<Student> query, SchoolDbContext db, Guid? selectedPeriodId)
    {
        if (SelectedProgramFilter?.ProgramId is int selectedProgramId)
        {
            query = query.Where(s => db.Enrollments.Any(e =>
                e.StudentId == s.StudentId &&
                e.ProgramId == selectedProgramId &&
                (selectedPeriodId == null || e.AcademicPeriodId == selectedPeriodId)));
        }

        query = SelectedStudentStatusFilter switch
        {
            ActiveStudentsFilter => selectedPeriodId == null
                ? query.Where(_ => false)
                : query.Where(s => db.Enrollments.Any(e => e.StudentId == s.StudentId && e.AcademicPeriodId == selectedPeriodId && !e.IsStopped)),
            InactiveStudentsFilter => selectedPeriodId == null
                ? query
                : query.Where(s => !db.Enrollments.Any(e => e.StudentId == s.StudentId && e.AcademicPeriodId == selectedPeriodId && !e.IsStopped)),
            ContractPendingFilter => selectedPeriodId == null
                ? query.Where(_ => false)
                : query.Where(s => db.Contracts.Any(c => c.StudentId == s.StudentId && c.Enrollment.AcademicPeriodId == selectedPeriodId && string.IsNullOrWhiteSpace(c.PdfPath))),
            OverdueFilter => selectedPeriodId == null
                ? query.Where(_ => false)
                : query.Where(s => db.Enrollments.Any(e => e.StudentId == s.StudentId && e.AcademicPeriodId == selectedPeriodId && !e.IsStopped && (e.InstallmentCount > 0 && e.InstallmentStartMonth != null))),
            DiscontinuedFilter => selectedPeriodId == null
                ? query.Where(_ => false)
                : query.Where(s => db.Enrollments.Any(e => e.StudentId == s.StudentId && e.AcademicPeriodId == selectedPeriodId && e.IsStopped)),
            _ => query
        };

        return query;
    }

    private async Task LoadSearchSuggestionsAsync(SchoolDbContext db, Guid? selectedPeriodId, int generation)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
            return;

        var term = SearchText.Trim();

        var filteredStudents = ApplyActiveFilters(db.Students.AsNoTracking(), db, selectedPeriodId);

        var suggestions = await filteredStudents
            .Where(s =>
                (s.FirstName + " " + s.LastName).Contains(term) ||
                s.Mobile.Contains(term) ||
                s.Landline.Contains(term) ||
                s.Email.Contains(term) ||
                db.Enrollments.Any(e =>
                    e.StudentId == s.StudentId &&
                    (selectedPeriodId == null || e.AcademicPeriodId == selectedPeriodId) &&
                    ((e.LevelOrClass != null && e.LevelOrClass.Contains(term)) || e.Program.Name.Contains(term))))
            .OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .Select(s => (s.FirstName + " " + s.LastName).Trim())
            .Distinct()
            .Take(8)
            .ToListAsync();

        if (generation != Volatile.Read(ref _loadGeneration))
            return;

        SearchSuggestions.Clear();
        foreach (var suggestion in suggestions)
            SearchSuggestions.Add(suggestion);

        if (_suppressSuggestionsOpenOnce)
        {
            _suppressSuggestionsOpenOnce = false;
            IsSearchSuggestionsOpen = false;
            return;
        }

        IsSearchSuggestionsOpen = SearchSuggestions.Count > 0;
    }

    private static string FormatCurrency(decimal amount)
        => amount.ToString("#,##0.#", new CultureInfo("el-GR")) + " €";

    private static string ToSurnameFirst(string? fullName)
    {
        var parts = (fullName ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length < 2)
            return fullName?.Trim() ?? string.Empty;

        var surname = parts[^1];
        var firstNames = string.Join(' ', parts[..^1]);
        return string.IsNullOrWhiteSpace(firstNames) ? surname : $"{surname} {firstNames}";
    }

    private static string BuildPreferredContactLine(Student student)
    {
        var fatherEmail = student.FatherEmail;
        var motherEmail = student.MotherEmail;

        var mobile = student.PreferredPhoneSource switch
        {
            PreferredPhoneSource.Father => student.FatherMobile,
            PreferredPhoneSource.Mother => student.MotherMobile,
            _ => student.Mobile
        };

        if (string.IsNullOrWhiteSpace(mobile))
        {
            mobile = !string.IsNullOrWhiteSpace(student.Mobile) ? student.Mobile
                : !string.IsNullOrWhiteSpace(student.FatherMobile) ? student.FatherMobile
                : student.MotherMobile;
        }

        var landline = student.PreferredLandlineSource switch
        {
            PreferredLandlineSource.Father => student.FatherLandline,
            PreferredLandlineSource.Mother => student.MotherLandline,
            _ => student.Landline
        };

        if (string.IsNullOrWhiteSpace(landline))
        {
            landline = !string.IsNullOrWhiteSpace(student.Landline) ? student.Landline
                : !string.IsNullOrWhiteSpace(student.FatherLandline) ? student.FatherLandline
                : student.MotherLandline;
        }

        var email = student.PreferredEmailSource switch
        {
            PreferredEmailSource.Father => fatherEmail,
            PreferredEmailSource.Mother => motherEmail,
            _ => student.Email
        };

        if (string.IsNullOrWhiteSpace(email))
        {
            email = !string.IsNullOrWhiteSpace(student.Email) ? student.Email
                : !string.IsNullOrWhiteSpace(fatherEmail) ? fatherEmail
                : motherEmail;
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(mobile))
            parts.Add(mobile);
        if (!string.IsNullOrWhiteSpace(landline))
            parts.Add(landline);
        if (!string.IsNullOrWhiteSpace(email))
            parts.Add(email);

        return parts.Count == 0 ? "—" : string.Join("  |  ", parts);
    }

}

public partial class ProgramFilterItemVm(int? programId, string name) : ObservableObject
{
    public int? ProgramId { get; } = programId;
    public string Name { get; } = name;

    [ObservableProperty] private bool isSelected;
}
