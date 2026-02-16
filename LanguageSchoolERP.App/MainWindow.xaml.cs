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

    public MainWindow(AppState state, DbContextFactory dbFactory)
    {
        InitializeComponent();
        _state = state;
        _dbFactory = dbFactory;

        DbCombo.ItemsSource = new[]
        {
            "FilotheiSchoolERP",
            "NeaIoniaSchoolERP"
        };
        DbCombo.SelectedItem = _state.SelectedDatabaseName;

        YearCombo.ItemsSource = new[]
        {
            "2024-2025",
            "2025-2026"
        };
        YearCombo.SelectedItem = _state.SelectedAcademicYear;

        DbCombo.SelectionChanged += async (_, __) =>
        {
            _state.SelectedDatabaseName = DbCombo.SelectedItem?.ToString() ?? _state.SelectedDatabaseName;
            await RefreshAcademicYearProgressAsync();
        };

        YearCombo.SelectionChanged += async (_, __) =>
        {
            _state.SelectedAcademicYear = YearCombo.SelectedItem?.ToString() ?? _state.SelectedAcademicYear;
            await RefreshAcademicYearProgressAsync();
        };

        // Default screen
        NavigateToStudents();

        StudentsBtn.Click += (_, __) => NavigateToStudents();
        ProgramsBtn.Click += (_, __) => NavigateToPrograms();
        AcademicYearsBtn.Click += (_, __) => NavigateToAcademicYears();

        _ = RefreshAcademicYearProgressAsync();
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
            YearProgressText.Text = "Year progress: 0%";
            return;
        }

        var enrollments = await db.Enrollments
            .AsNoTracking()
            .Include(e => e.Payments)
            .Where(e => e.AcademicPeriodId == period.AcademicPeriodId)
            .ToListAsync();

        decimal agreementSum = enrollments.Sum(e => e.AgreementTotal);
        decimal paidSum = enrollments.Sum(e => e.DownPayment + e.Payments.Sum(p => p.Amount));

        var progress = agreementSum <= 0 ? 0d : (double)(paidSum / agreementSum * 100m);
        if (progress > 100) progress = 100;
        if (progress < 0) progress = 0;

        YearProgressBar.Value = progress;
        YearProgressText.Text = $"Year progress: {progress:0}%";
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
