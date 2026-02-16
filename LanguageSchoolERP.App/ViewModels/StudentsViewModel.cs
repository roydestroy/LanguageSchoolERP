using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanguageSchoolERP.Core.Models;
using LanguageSchoolERP.Services;
using Microsoft.EntityFrameworkCore;
using LanguageSchoolERP.App.Windows;
using Microsoft.Extensions.DependencyInjection;


namespace LanguageSchoolERP.App.ViewModels;

public partial class StudentsViewModel : ObservableObject
{
    private const string AllStudentsFilter = "All students";
    private const string ActiveStudentsFilter = "Active only";
    private const string InactiveStudentsFilter = "Inactive only";
    private const string ContractPendingFilter = "Contract pending";
    private const string OverdueFilter = "Overdue";

    private readonly AppState _state;
    private readonly DbContextFactory _dbFactory;

    public ObservableCollection<StudentRowVm> Students { get; } = new();
    public ObservableCollection<string> StudentStatusFilters { get; } =
    [
        AllStudentsFilter,
        ActiveStudentsFilter,
        InactiveStudentsFilter,
        ContractPendingFilter,
        OverdueFilter
    ];

    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private string selectedStudentStatusFilter = AllStudentsFilter;

    public IRelayCommand RefreshCommand { get; }
    public IRelayCommand NewStudentCommand { get; }
    public IRelayCommand<Guid> OpenStudentCommand { get; }

    public StudentsViewModel(AppState state, DbContextFactory dbFactory)
    {
        _state = state;
        _dbFactory = dbFactory;

        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        NewStudentCommand = new RelayCommand(OpenNewStudentDialog);
        OpenStudentCommand = new RelayCommand<Guid>(OpenStudent);

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
    }

    partial void OnSearchTextChanged(string value)
    {
        // lightweight debounce not needed yet; refresh on typing is fine for now
        _ = LoadAsync();
    }

    partial void OnSelectedStudentStatusFilterChanged(string value)
    {
        _ = LoadAsync();
    }

    private void OpenNewStudentDialog()
    {
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

        win.Closed += (_, __) => _ = LoadAsync();
        win.Show();

    }

    private async Task LoadAsync()
    {
        try
        {
            Students.Clear();

            using var db = _dbFactory.Create();
            System.Diagnostics.Debug.WriteLine("DB=" + db.Database.GetDbConnection().DataSource + " | " + db.Database.GetDbConnection().Database);

            DbSeeder.EnsureSeeded(db);

            var year = _state.SelectedAcademicYear;

            // Selected academic period is optional; students should remain visible even if no period exists.
            var period = await db.AcademicPeriods
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Name == year);
            var selectedPeriodId = period?.AcademicPeriodId;

            var baseQuery = db.Students.AsNoTracking();

            baseQuery = SelectedStudentStatusFilter switch
            {
                ActiveStudentsFilter => selectedPeriodId == null
                    ? baseQuery.Where(_ => false)
                    : baseQuery.Where(s => db.Enrollments.Any(e => e.StudentId == s.StudentId && e.AcademicPeriodId == selectedPeriodId)),
                InactiveStudentsFilter => selectedPeriodId == null
                    ? baseQuery
                    : baseQuery.Where(s => !db.Enrollments.Any(e => e.StudentId == s.StudentId && e.AcademicPeriodId == selectedPeriodId)),
                ContractPendingFilter => selectedPeriodId == null
                    ? baseQuery.Where(_ => false)
                    : baseQuery.Where(s => db.Contracts.Any(c => c.StudentId == s.StudentId && c.Enrollment.AcademicPeriodId == selectedPeriodId && string.IsNullOrWhiteSpace(c.PdfPath))),
                OverdueFilter => selectedPeriodId == null
                    ? baseQuery.Where(_ => false)
                    : baseQuery.Where(s => db.Enrollments.Any(e => e.StudentId == s.StudentId && e.AcademicPeriodId == selectedPeriodId && (e.InstallmentCount > 0 && e.InstallmentStartMonth != null))),
                _ => baseQuery
            };

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var st = SearchText.Trim();
                baseQuery = baseQuery.Where(s =>
                    s.FullName.Contains(st) ||
                    s.Phone.Contains(st) ||
                    s.Email.Contains(st));
            }

            var students = await baseQuery
                .Include(s => s.Enrollments.Where(en => selectedPeriodId == null || en.AcademicPeriodId == selectedPeriodId))
                    .ThenInclude(en => en.Payments)
                .Include(s => s.Contracts.Where(c => selectedPeriodId == null || c.Enrollment.AcademicPeriodId == selectedPeriodId))
                .OrderBy(s => s.FullName)
                .ToListAsync();

            foreach (var s in students)
            {
                // Aggregate across enrollments in selected year
                var yearEnrollments = s.Enrollments.ToList();

                decimal agreementSum = yearEnrollments.Sum(en => en.AgreementTotal);
                decimal downSum = yearEnrollments.Sum(en => en.DownPayment);
                decimal paidSum = yearEnrollments.Sum(en => en.Payments.Sum(p => p.Amount));

                var balance = agreementSum - (downSum + paidSum);
                if (balance < 0) balance = 0;

                var today = DateTime.Today;

                // Overdue if ANY enrollment is overdue based on installment plan
                bool overdue = yearEnrollments.Any(en => InstallmentPlanHelper.IsEnrollmentOverdue(en, today));
                bool hasPendingContract = s.Contracts.Any(c => string.IsNullOrWhiteSpace(c.PdfPath));

                if (SelectedStudentStatusFilter == OverdueFilter && !overdue)
                    continue;

                if (SelectedStudentStatusFilter == ContractPendingFilter && !hasPendingContract)
                    continue;


                var row = new StudentRowVm
                {
                    StudentId = s.StudentId,
                    FullName = s.FullName,
                    ContactLine = $"{s.Phone}  |  {s.Email}".Trim(' ', '|'),
                    YearLabel = $"Year: {year}",
                    Balance = balance,
                    IsOverdue = overdue,
                    HasPendingContract = hasPendingContract,
                    IsActive = yearEnrollments.Count > 0,
                    IsExpanded = false
                };

                foreach (var en in yearEnrollments.OrderBy(x => x.ProgramType))
                {
                    var enPaid = en.Payments.Sum(p => p.Amount) + en.DownPayment;
                    var enBalance = en.AgreementTotal - enPaid;
                    if (enBalance < 0) enBalance = 0;

                    row.Enrollments.Add(new EnrollmentRowVm
                    {
                        Title = en.ProgramType.ToDisplayName(),
                        Details = string.IsNullOrWhiteSpace(en.LevelOrClass) ? "" : $"Level/Class: {en.LevelOrClass}",
                        AgreementText = $"Agreement: {en.AgreementTotal:0.00} €",
                        PaidText = $"Paid: {enPaid:0.00} €",
                        BalanceText = $"Balance: {enBalance:0.00} €"
                    });
                }

                Students.Add(row);
            }
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            System.Diagnostics.Debug.WriteLine(ex.ToString());
            System.Windows.MessageBox.Show(msg, "Load students failed");
        }

    }
}
