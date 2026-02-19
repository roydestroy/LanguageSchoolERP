using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanguageSchoolERP.Services;
using Microsoft.EntityFrameworkCore;

namespace LanguageSchoolERP.App.ViewModels;

public partial class StatisticsViewModel : ObservableObject
{
    private readonly DbContextFactory _dbFactory;
    private readonly AppState _state;

    public ObservableCollection<ProgramStatisticsRowVm> ProgramStatistics { get; } = new();

    [ObservableProperty] private string selectedAcademicYear = string.Empty;
    [ObservableProperty] private int studentsCount;
    [ObservableProperty] private int activeStudentsCount;
    [ObservableProperty] private int enrollmentsCount;
    [ObservableProperty] private int discontinuedEnrollmentsCount;
    [ObservableProperty] private decimal agreementsTotal;
    [ObservableProperty] private decimal collectibleTotal;
    [ObservableProperty] private decimal collectedTotal;
    [ObservableProperty] private decimal outstandingTotal;
    [ObservableProperty] private decimal lostRevenueTotal;
    [ObservableProperty] private double collectibleProgressPercent;
    [ObservableProperty] private double agreementProgressPercent;
    [ObservableProperty] private string errorMessage = string.Empty;

    public IAsyncRelayCommand RefreshCommand { get; }

    public StatisticsViewModel(DbContextFactory dbFactory, AppState state)
    {
        _dbFactory = dbFactory;
        _state = state;

        RefreshCommand = new AsyncRelayCommand(LoadAsync);

        _state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.SelectedAcademicYear) ||
                e.PropertyName == nameof(AppState.SelectedDatabaseMode) ||
                e.PropertyName == nameof(AppState.SelectedDatabaseName) ||
                e.PropertyName == nameof(AppState.DataVersion))
            {
                _ = LoadAsync();
            }
        };

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            ErrorMessage = string.Empty;
            SelectedAcademicYear = _state.SelectedAcademicYear;

            await using var db = _dbFactory.Create();
            DbSeeder.EnsureSeeded(db);

            var period = await db.AcademicPeriods
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Name == _state.SelectedAcademicYear);

            if (period is null)
            {
                ResetStatistics();
                return;
            }

            var enrollments = await db.Enrollments
                .AsNoTracking()
                .Where(e => e.AcademicPeriodId == period.AcademicPeriodId)
                .Include(e => e.Program)
                .Include(e => e.Student)
                .Include(e => e.Payments)
                .ToListAsync();

            StudentsCount = enrollments
                .Select(e => e.StudentId)
                .Distinct()
                .Count();

            ActiveStudentsCount = enrollments
                .Where(e => !e.Student.Discontinued)
                .Select(e => e.StudentId)
                .Distinct()
                .Count();

            EnrollmentsCount = enrollments.Count;
            DiscontinuedEnrollmentsCount = enrollments.Count(e => e.IsStopped);

            AgreementsTotal = enrollments.Sum(InstallmentPlanHelper.GetEffectiveAgreementTotal);
            CollectedTotal = enrollments.Sum(e => e.DownPayment + PaymentAgreementHelper.SumAgreementPayments(e.Payments));
            LostRevenueTotal = enrollments.Sum(InstallmentPlanHelper.GetLostAmount);

            var discontinuedRemaining = enrollments
                .Where(e => e.IsStopped)
                .Sum(InstallmentPlanHelper.GetOutstandingAmount);

            CollectibleTotal = Math.Max(0m, AgreementsTotal - discontinuedRemaining);
            OutstandingTotal = Math.Max(0m, CollectibleTotal - CollectedTotal);

            CollectibleProgressPercent = CollectibleTotal <= 0m
                ? 0d
                : Math.Clamp((double)(CollectedTotal / CollectibleTotal * 100m), 0d, 100d);
            AgreementProgressPercent = AgreementsTotal <= 0m
                ? 0d
                : Math.Clamp((double)(CollectedTotal / AgreementsTotal * 100m), 0d, 100d);

            var rows = enrollments
                .GroupBy(e => new { e.ProgramId, ProgramName = e.Program.Name })
                .Select(g =>
                {
                    var agreementTotal = g.Sum(InstallmentPlanHelper.GetEffectiveAgreementTotal);
                    var collectedTotal = g.Sum(x => x.DownPayment + PaymentAgreementHelper.SumAgreementPayments(x.Payments));
                    var discontinuedRemainingByProgram = g.Where(x => x.IsStopped).Sum(InstallmentPlanHelper.GetOutstandingAmount);
                    var collectibleTotal = Math.Max(0m, agreementTotal - discontinuedRemainingByProgram);

                    return new ProgramStatisticsRowVm
                    {
                        ProgramName = g.Key.ProgramName,
                        StudentsCount = g.Select(x => x.StudentId).Distinct().Count(),
                        EnrollmentsCount = g.Count(),
                        AgreementTotal = agreementTotal,
                        CollectedTotal = collectedTotal,
                        OutstandingTotal = Math.Max(0m, collectibleTotal - collectedTotal)
                    };
                })
                .OrderByDescending(x => x.StudentsCount)
                .ThenBy(x => x.ProgramName)
                .ToList();

            ProgramStatistics.Clear();
            foreach (var row in rows)
            {
                ProgramStatistics.Add(row);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Αποτυχία φόρτωσης στατιστικών: {ex.Message}";
            ResetStatistics();
        }
    }

    private void ResetStatistics()
    {
        StudentsCount = 0;
        ActiveStudentsCount = 0;
        EnrollmentsCount = 0;
        DiscontinuedEnrollmentsCount = 0;
        AgreementsTotal = 0;
        CollectibleTotal = 0;
        CollectedTotal = 0;
        OutstandingTotal = 0;
        LostRevenueTotal = 0;
        CollectibleProgressPercent = 0;
        AgreementProgressPercent = 0;
        ProgramStatistics.Clear();
    }
}
