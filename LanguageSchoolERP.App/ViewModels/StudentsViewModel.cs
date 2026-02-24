using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Collections.Concurrent;
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
using System.Windows.Threading;


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
    private int _searchDebounceVersion;
    private int _latestRequestedGeneration;
    private int _isLoadLoopRunning;
    private bool _suppressProgramFilterReload;
    private bool _suppressSuggestionsOpenOnce;
    private readonly ConcurrentDictionary<Guid, byte> _detailsLoadInFlight = new();

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
    [ObservableProperty] private bool isLoading;

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

        RefreshCommand = new AsyncRelayCommand(() => StartLatestLoadAsync());
        NewStudentCommand = new RelayCommand(OpenNewStudentDialog, CanCreateStudent);
        OpenStudentCommand = new RelayCommand<Guid>(OpenStudent);
        QuickAddPaymentCommand = new RelayCommand<Guid>(OpenQuickPaymentDialog, CanOpenQuickPayment);
        SelectProgramFilterCommand = new RelayCommand<ProgramFilterItemVm>(SelectProgramFilter);
        ApplySearchSuggestionCommand = new RelayCommand<string>(ApplySearchSuggestion);

        Students.CollectionChanged += OnStudentsCollectionChanged;

        var allProgramsFilter = new ProgramFilterItemVm(null, "ΟΛΑ") { IsSelected = true };
        ProgramFilters.Add(allProgramsFilter);
        selectedProgramFilter = allProgramsFilter;

        // Refresh automatically when DB/year changes
        _state.PropertyChanged += OnAppStateChanged;

        // Initial load
        _ = StartLatestLoadAsync();
    }

    private void OnAppStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppState.SelectedAcademicYear) ||
            e.PropertyName == nameof(AppState.SelectedDatabaseName))
        {
            _ = StartLatestLoadAsync();
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

        _ = ScheduleSearchLoad();
    }

    partial void OnSelectedStudentStatusFilterChanged(string value)
    {
        _ = StartLatestLoadAsync();
    }

    partial void OnSelectedStudentSortOptionChanged(string value)
    {
        _ = StartLatestLoadAsync();
    }

    partial void OnSelectedProgramFilterChanged(ProgramFilterItemVm? value)
    {
        foreach (var item in ProgramFilters)
            item.IsSelected = ReferenceEquals(item, value);

        if (_suppressProgramFilterReload)
            return;

        _ = StartLatestLoadAsync();
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
            await StartLatestLoadAsync();
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
            _ = StartLatestLoadAsync();
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

    private Task ScheduleSearchLoad()
    {
        var debounceVersion = Interlocked.Increment(ref _searchDebounceVersion);
        return DebouncedLoadAsync(debounceVersion);
    }

    private async Task DebouncedLoadAsync(int debounceVersion)
    {
        await Task.Delay(250);

        if (debounceVersion != Volatile.Read(ref _searchDebounceVersion))
            return;

        await StartLatestLoadAsync();
    }

    private Task StartLatestLoadAsync()
    {
        var generation = Interlocked.Increment(ref _loadGeneration);
        Volatile.Write(ref _latestRequestedGeneration, generation);

        if (Interlocked.CompareExchange(ref _isLoadLoopRunning, 1, 0) != 0)
            return Task.CompletedTask;

        IsLoading = true;

        return ProcessLatestLoadsAsync();
    }

    private async Task ProcessLatestLoadsAsync()
    {
        var processedGeneration = 0;

        try
        {
            while (true)
            {
                var generationToProcess = Volatile.Read(ref _latestRequestedGeneration);
                processedGeneration = generationToProcess;

                await LoadAsync(generationToProcess);

                if (generationToProcess == Volatile.Read(ref _latestRequestedGeneration))
                    break;
            }
        }
        finally
        {
            Interlocked.Exchange(ref _isLoadLoopRunning, 0);

            var shouldRestart = processedGeneration != Volatile.Read(ref _latestRequestedGeneration) &&
                Interlocked.CompareExchange(ref _isLoadLoopRunning, 1, 0) == 0;

            if (shouldRestart)
            {
                _ = ProcessLatestLoadsAsync();
            }
            else
            {
                IsLoading = false;
            }
        }
    }

    private async Task LoadAsync(int generation)
    {
        try
        {
            using var db = _dbFactory.Create();
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

            var today = DateTime.Today;
            var currentMonthStart = new DateTime(today.Year, today.Month, 1);

            var summaryRows = await baseQuery
                .OrderBy(s => s.LastName)
                .ThenBy(s => s.FirstName)
                .Select(s => new StudentSummaryProjection
                {
                    StudentId = s.StudentId,
                    FirstName = s.FirstName,
                    LastName = s.LastName,
                    Address = s.Address,
                    Mobile = s.Mobile,
                    Landline = s.Landline,
                    Email = s.Email,
                    FatherMobile = s.FatherMobile,
                    FatherLandline = s.FatherLandline,
                    FatherEmail = s.FatherEmail,
                    MotherMobile = s.MotherMobile,
                    MotherLandline = s.MotherLandline,
                    MotherEmail = s.MotherEmail,
                    PreferredPhoneSource = s.PreferredPhoneSource,
                    PreferredLandlineSource = s.PreferredLandlineSource,
                    PreferredEmailSource = s.PreferredEmailSource,
                    HasPendingContract = db.Contracts.Any(c => c.StudentId == s.StudentId && (selectedPeriodId == null || c.Enrollment.AcademicPeriodId == selectedPeriodId) && string.IsNullOrWhiteSpace(c.PdfPath)),
                    Enrollments = db.Enrollments
                        .Where(e => e.StudentId == s.StudentId && (selectedPeriodId == null || e.AcademicPeriodId == selectedPeriodId))
                        .Select(e => new EnrollmentSummaryProjection
                        {
                            IsStopped = e.IsStopped,
                            ProgramName = e.Program.Name,
                            LevelOrClass = e.LevelOrClass,
                            AgreementTotal = e.AgreementTotal,
                            DownPayment = e.DownPayment,
                            IncludesStudyLab = e.IncludesStudyLab,
                            StudyLabMonthlyPrice = e.StudyLabMonthlyPrice,
                            InstallmentCount = e.InstallmentCount,
                            InstallmentStartMonth = e.InstallmentStartMonth,
                            PaidAmount = e.Payments
                                .Where(p => (p.Notes == null || (!EF.Functions.Like(p.Notes, "%[ΑΚΥΡΩΜΕΝΗ_ΠΛΗΡΩΜΗ]%") && !EF.Functions.Like(p.Notes, "%[ΕΚΤΟΣ_ΣΥΜΦΩΝΗΘΕΝΤΟΣ]%") && !EF.Functions.Like(p.Notes, "%[EXCLUDE_FROM_AGREEMENT]%"))))
                                .Sum(p => (decimal?)p.Amount) ?? 0m
                        })
                        .ToList()
                })
                .ToListAsync();

            var rows = new List<StudentRowVm>();

            foreach (var s in summaryRows)
            {
                var yearEnrollments = s.Enrollments;
                var activeEnrollments = yearEnrollments.Where(en => !en.IsStopped).ToList();

                decimal activeAgreementSum = activeEnrollments.Sum(GetEffectiveAgreementTotal);
                decimal activeDownSum = activeEnrollments.Sum(en => en.DownPayment);
                decimal activePaidSum = activeEnrollments.Sum(en => en.PaidAmount);

                var totalProgress = activeAgreementSum <= 0 ? 0d : (double)((activeDownSum + activePaidSum) / activeAgreementSum * 100m);
                totalProgress = Math.Clamp(totalProgress, 0d, 100d);

                bool overdue = yearEnrollments.Any(en => IsEnrollmentOverdue(en, today, currentMonthStart));

                var overdueAmount = yearEnrollments
                    .Where(en => IsEnrollmentOverdue(en, today, currentMonthStart))
                    .Sum(GetOutstandingAmount);

                var hasStoppedProgram = yearEnrollments.Any(en => en.IsStopped);
                var hasOnlyStoppedPrograms = yearEnrollments.Count > 0 && yearEnrollments.All(en => en.IsStopped);

                var activeBalance = activeEnrollments.Sum(e => GetEffectiveAgreementTotal(e) - (e.DownPayment + e.PaidAmount));
                var anyActiveOverdue = activeEnrollments.Any(en => IsEnrollmentOverdue(en, today, currentMonthStart));
                var anyActiveOverpaid = activeEnrollments.Any(en => (en.DownPayment + en.PaidAmount) > GetEffectiveAgreementTotal(en) + 0.009m);
                var allActiveFullyPaid = activeEnrollments.Count > 0 && activeEnrollments.All(en => (en.DownPayment + en.PaidAmount) + 0.009m >= GetEffectiveAgreementTotal(en));

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
                    .OrderBy(en => en.ProgramName)
                    .Select(en => string.IsNullOrWhiteSpace(en.LevelOrClass) ? (en.ProgramName ?? "—") : $"{en.ProgramName ?? "—"} ({en.LevelOrClass})")
                    .ToList();

                var enrollmentSummaryText = activeEnrollmentSummaryItems.Count == 0
                    ? "Προγράμματα: —"
                    : $"Προγράμματα: {string.Join(" · ", activeEnrollmentSummaryItems)}";

                rows.Add(new StudentRowVm
                {
                    StudentId = s.StudentId,
                    FullName = ToSurnameFirst($"{s.FirstName} {s.LastName}"),
                    ContactLine = BuildPreferredContactLine(s),
                    Address = s.Address ?? string.Empty,
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
                    HasPendingContract = s.HasPendingContract,
                    IsActive = activeEnrollments.Count > 0,
                    IsExpanded = false,
                    AreDetailsLoaded = false,
                    IsDetailsLoading = false
                });
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

            await RunOnUiThreadAsync(() =>
            {
                Students.Clear();
                foreach (var row in sortedRows)
                    Students.Add(row);
            });

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


    private void OnStudentsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<StudentRowVm>())
                item.PropertyChanged -= OnStudentRowPropertyChanged;
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<StudentRowVm>())
                item.PropertyChanged += OnStudentRowPropertyChanged;
        }

        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var student in Students)
                student.PropertyChanged += OnStudentRowPropertyChanged;
        }
    }

    private void OnStudentRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(StudentRowVm.IsExpanded) || sender is not StudentRowVm row || !row.IsExpanded)
            return;

        _ = LoadStudentDetailsAsync(row);
    }

    private async Task LoadStudentDetailsAsync(StudentRowVm row)
    {
        if (row.AreDetailsLoaded)
            return;

        if (!_detailsLoadInFlight.TryAdd(row.StudentId, 0))
            return;

        await RunOnUiThreadAsync(() => row.IsDetailsLoading = true);

        try
        {
            using var db = _dbFactory.Create();
            DbSeeder.EnsureSeeded(db);

            var selectedPeriodId = await db.AcademicPeriods
                .AsNoTracking()
                .Where(p => p.Name == _state.SelectedAcademicYear)
                .Select(p => (Guid?)p.AcademicPeriodId)
                .FirstOrDefaultAsync();

            var enrollments = await db.Enrollments
                .AsNoTracking()
                .Where(e => e.StudentId == row.StudentId && (selectedPeriodId == null || e.AcademicPeriodId == selectedPeriodId))
                .Include(e => e.Program)
                .Include(e => e.Payments)
                .OrderBy(e => e.Program!.Name)
                .ToListAsync();

            var contracts = await db.Contracts
                .AsNoTracking()
                .Where(c => c.StudentId == row.StudentId && (selectedPeriodId == null || c.Enrollment.AcademicPeriodId == selectedPeriodId))
                .Select(c => c.PdfPath)
                .ToListAsync();

            var hasPendingContract = contracts.Any(string.IsNullOrWhiteSpace);

            var today = DateTime.Today;
            var details = new List<EnrollmentRowVm>();
            foreach (var en in enrollments)
            {
                var enPaid = PaymentAgreementHelper.SumAgreementPayments(en.Payments) + en.DownPayment;
                var enrollmentEffectiveTotal = InstallmentPlanHelper.GetEffectiveAgreementTotal(en);
                var enBalance = enrollmentEffectiveTotal - enPaid;
                var enOverdue = InstallmentPlanHelper.IsEnrollmentOverdue(en, today);
                var enOverpaid = enPaid > enrollmentEffectiveTotal + 0.009m;
                var enFullyPaid = enPaid + 0.009m >= enrollmentEffectiveTotal;

                var enrollmentProgress = enrollmentEffectiveTotal <= 0 ? 0d : (double)(enPaid / enrollmentEffectiveTotal * 100m);
                enrollmentProgress = Math.Clamp(enrollmentProgress, 0d, 100d);

                var enrollmentProgressBrush = en.IsStopped
                    ? ProgressStoppedRedBrush
                    : enOverpaid
                        ? ProgressPurpleBrush
                        : enOverdue
                            ? ProgressOrangeBrush
                            : enFullyPaid
                                ? ProgressGreenBrush
                                : ProgressBlueBrush;

                details.Add(new EnrollmentRowVm
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

            await RunOnUiThreadAsync(() =>
            {
                row.HasPendingContract = hasPendingContract;
                row.Enrollments.Clear();
                foreach (var detail in details)
                    row.Enrollments.Add(detail);

                row.AreDetailsLoaded = true;
            });
        }
        finally
        {
            await RunOnUiThreadAsync(() => row.IsDetailsLoading = false);
            _detailsLoadInFlight.TryRemove(row.StudentId, out _);
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

        await RunOnUiThreadAsync(() =>
        {
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
        });
    }


    private static decimal GetEffectiveAgreementTotal(EnrollmentSummaryProjection enrollment)
        => enrollment.AgreementTotal + (enrollment.IncludesStudyLab ? enrollment.StudyLabMonthlyPrice.GetValueOrDefault() : 0m);

    private static decimal GetOutstandingAmount(EnrollmentSummaryProjection enrollment)
    {
        var remaining = GetEffectiveAgreementTotal(enrollment) - (enrollment.DownPayment + enrollment.PaidAmount);
        return remaining > 0 ? remaining : 0m;
    }

    private static bool IsEnrollmentOverdue(EnrollmentSummaryProjection enrollment, DateTime today, DateTime currentMonthStart)
    {
        if (enrollment.IsStopped)
            return false;

        if (enrollment.InstallmentCount <= 0 || enrollment.InstallmentStartMonth is null)
            return false;

        var start = new DateTime(enrollment.InstallmentStartMonth.Value.Year, enrollment.InstallmentStartMonth.Value.Month, 1);
        if (today < start)
            return false;

        var paid = enrollment.DownPayment + enrollment.PaidAmount;
        var remaining = GetEffectiveAgreementTotal(enrollment) - paid;
        if (remaining <= 0)
            return false;

        var monthsElapsed = (currentMonthStart.Year - start.Year) * 12 + (currentMonthStart.Month - start.Month) + 1;
        var installmentsDue = Math.Min(enrollment.InstallmentCount, Math.Max(0, monthsElapsed));
        if (installmentsDue <= 0)
            return false;

        var financedAmount = GetEffectiveAgreementTotal(enrollment) - enrollment.DownPayment;
        if (financedAmount <= 0)
            return false;

        var roundedFinancedAmount = Math.Round(financedAmount, 0, MidpointRounding.AwayFromZero);
        var baseAmount = Math.Floor(roundedFinancedAmount / enrollment.InstallmentCount);
        var expectedPaid = enrollment.DownPayment;

        for (var i = 0; i < installmentsDue; i++)
        {
            expectedPaid += i == enrollment.InstallmentCount - 1
                ? roundedFinancedAmount - (baseAmount * (enrollment.InstallmentCount - 1))
                : baseAmount;
        }

        return paid + 0.009m < expectedPaid;
    }

    private static Task RunOnUiThreadAsync(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(action, DispatcherPriority.DataBind).Task;
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

    private static string BuildPreferredContactLine(StudentContactProjection student)
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


public interface StudentContactProjection
{
    string? Mobile { get; }
    string? Landline { get; }
    string? Email { get; }
    string? FatherMobile { get; }
    string? FatherLandline { get; }
    string? FatherEmail { get; }
    string? MotherMobile { get; }
    string? MotherLandline { get; }
    string? MotherEmail { get; }
    PreferredPhoneSource PreferredPhoneSource { get; }
    PreferredLandlineSource PreferredLandlineSource { get; }
    PreferredEmailSource PreferredEmailSource { get; }
}

internal sealed class StudentSummaryProjection : StudentContactProjection
{
    public Guid StudentId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Mobile { get; set; }
    public string? Landline { get; set; }
    public string? Email { get; set; }
    public string? FatherMobile { get; set; }
    public string? FatherLandline { get; set; }
    public string? FatherEmail { get; set; }
    public string? MotherMobile { get; set; }
    public string? MotherLandline { get; set; }
    public string? MotherEmail { get; set; }
    public PreferredPhoneSource PreferredPhoneSource { get; set; }
    public PreferredLandlineSource PreferredLandlineSource { get; set; }
    public PreferredEmailSource PreferredEmailSource { get; set; }
    public bool HasPendingContract { get; set; }
    public List<EnrollmentSummaryProjection> Enrollments { get; set; } = [];
}

internal sealed class EnrollmentSummaryProjection
{
    public bool IsStopped { get; set; }
    public string? ProgramName { get; set; }
    public string? LevelOrClass { get; set; }
    public decimal AgreementTotal { get; set; }
    public decimal DownPayment { get; set; }
    public bool IncludesStudyLab { get; set; }
    public decimal? StudyLabMonthlyPrice { get; set; }
    public int InstallmentCount { get; set; }
    public DateTime? InstallmentStartMonth { get; set; }
    public decimal PaidAmount { get; set; }
}

public partial class ProgramFilterItemVm(int? programId, string name) : ObservableObject
{
    public int? ProgramId { get; } = programId;
    public string Name { get; } = name;

    [ObservableProperty] private bool isSelected;
}
