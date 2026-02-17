using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using LanguageSchoolERP.Services;
using Microsoft.Extensions.DependencyInjection;
using LanguageSchoolERP.App.Views;
using Microsoft.EntityFrameworkCore;

namespace LanguageSchoolERP.App;

public partial class MainWindow : Window
{
    private readonly AppState _state;
    private readonly DbContextFactory _dbFactory;

    public MainWindow(AppState state, DbContextFactory dbFactory, DatabaseAppSettingsProvider settingsProvider)
    {
        InitializeComponent();
        _state = state;
        _dbFactory = dbFactory;

        ModeCombo.ItemsSource = new[] { DatabaseMode.Local, DatabaseMode.Remote };
        ModeCombo.SelectedItem = _state.SelectedDatabaseMode;

        DbCombo.ItemsSource = settingsProvider.RemoteDatabases;
        DbCombo.SelectedValue = _state.SelectedRemoteDatabaseName;

        YearCombo.ItemsSource = new[]
        {
            "2024-2025",
            "2025-2026"
        };
        YearCombo.SelectedItem = _state.SelectedAcademicYear;

        ModeCombo.SelectionChanged += async (_, __) =>
        {
            if (ModeCombo.SelectedItem is DatabaseMode mode)
            {
                _state.SelectedDatabaseMode = mode;
                SyncTopBarState();
                await RefreshAcademicYearProgressAsync();
            }
        };

        DbCombo.SelectionChanged += async (_, __) =>
        {
            if (DbCombo.SelectedValue is string selectedRemoteDb && !string.IsNullOrWhiteSpace(selectedRemoteDb))
            {
                _state.SelectedRemoteDatabaseName = selectedRemoteDb;
                await RefreshAcademicYearProgressAsync();
            }
        };

        YearCombo.SelectionChanged += async (_, __) =>
        {
            _state.SelectedAcademicYear = YearCombo.SelectedItem?.ToString() ?? _state.SelectedAcademicYear;
            await RefreshAcademicYearProgressAsync();
        };

        _state.PropertyChanged += OnAppStateChanged;
        SyncTopBarState();

        NavigateToStudents();

        StudentsBtn.Click += (_, __) => NavigateToStudents();
        ProgramsBtn.Click += (_, __) => NavigateToPrograms();
        AcademicYearsBtn.Click += (_, __) => NavigateToAcademicYears();

        _ = RefreshAcademicYearProgressAsync();
    }

    private void OnAppStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppState.SelectedDatabaseMode) ||
            e.PropertyName == nameof(AppState.SelectedRemoteDatabaseName))
        {
            SyncTopBarState();
        }
    }

    private void SyncTopBarState()
    {
        ModeCombo.SelectedItem = _state.SelectedDatabaseMode;
        DbCombo.SelectedValue = _state.SelectedRemoteDatabaseName;
        RemoteDbGrid.Visibility = _state.SelectedDatabaseMode == DatabaseMode.Remote
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private async Task RefreshAcademicYearProgressAsync()
    {
        using var db = _dbFactory.Create();

        var year = _state.SelectedAcademicYear;
        var period = await db.AcademicPeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Name == year);

        if (period is null)
        {
            YearProgressBar.Value = 0;
            YearProgressText.Text = "Πρόοδος Έτους: 0%";
            YearLostRevenueText.Text = "Απώλειες διακοπών: 0,00 €";
            return;
        }

        var enrollments = await db.Enrollments
            .AsNoTracking()
            .Include(e => e.Payments)
            .Where(e => e.AcademicPeriodId == period.AcademicPeriodId)
            .ToListAsync();

        decimal agreementSum = enrollments.Sum(InstallmentPlanHelper.GetEffectiveAgreementTotal);
        decimal paidSum = enrollments.Sum(e => e.DownPayment + e.Payments.Sum(p => p.Amount));
        decimal lostRevenue = enrollments.Sum(InstallmentPlanHelper.GetLostAmount);

        var progress = agreementSum <= 0 ? 0d : (double)(paidSum / agreementSum * 100m);
        if (progress > 100) progress = 100;
        if (progress < 0) progress = 0;

        YearProgressBar.Value = progress;
        YearProgressText.Text = $"Πρόοδος Έτους: {progress:0}%";
        YearLostRevenueText.Text = $"Απώλειες διακοπών: {lostRevenue:0.00} €";
    }

    private void NavigateToStudents()
    {
        var view = App.Services.GetRequiredService<StudentsView>();
        MainContent.Content = view;
    }

    private void NavigateToPrograms()
    {
        var view = App.Services.GetRequiredService<ProgramsView>();
        MainContent.Content = view;
    }

    private void NavigateToAcademicYears()
    {
        var view = App.Services.GetRequiredService<AcademicYearsView>();
        MainContent.Content = view;
    }
}
