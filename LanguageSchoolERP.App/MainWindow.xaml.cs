using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows;
using System.Globalization;
using System.Windows.Controls;
using LanguageSchoolERP.Services;
using Microsoft.Extensions.DependencyInjection;
using LanguageSchoolERP.App.Views;
using Microsoft.EntityFrameworkCore;

namespace LanguageSchoolERP.App;

public partial class MainWindow : Window
{
    private readonly AppState _state;
    private readonly DbContextFactory _dbFactory;
    private readonly IReadOnlyList<LocalDatabaseOption> _allLocalDatabases;

    private sealed class LocalDatabaseOption
    {
        public string Key { get; init; } = string.Empty;
        public string Database { get; init; } = string.Empty;
    }

    public MainWindow(AppState state, DbContextFactory dbFactory, DatabaseAppSettingsProvider settingsProvider)
    {
        InitializeComponent();
        _state = state;
        _dbFactory = dbFactory;

        _allLocalDatabases = new[]
        {
            new LocalDatabaseOption { Key = "Filothei", Database = "FilotheiSchoolERP" },
            new LocalDatabaseOption { Key = "Nea Ionia", Database = "NeaIoniaSchoolERP" }
        };

        RefreshModeOptions();
        ModeCombo.SelectedItem = _state.SelectedDatabaseMode;

        DbCombo.ItemsSource = settingsProvider.RemoteDatabases;
        DbCombo.SelectedValue = _state.SelectedRemoteDatabaseName;

        RefreshLocalDatabaseOptions();
        LocalDbCombo.SelectedValue = _state.SelectedLocalDatabaseName;

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
                if (mode == DatabaseMode.Local && !_state.IsLocalModeEnabled)
                {
                    _state.SelectedDatabaseMode = DatabaseMode.Remote;
                    ModeCombo.SelectedItem = DatabaseMode.Remote;
                    MessageBox.Show(
                        "Η local λειτουργία δεν είναι διαθέσιμη μέχρι να γίνει εισαγωγή τοπικής βάσης.",
                        "Τοπική βάση",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

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

        LocalDbCombo.SelectionChanged += async (_, __) =>
        {
            if (LocalDbCombo.SelectedValue is string selectedLocalDb && !string.IsNullOrWhiteSpace(selectedLocalDb))
            {
                _state.SelectedLocalDatabaseName = selectedLocalDb;
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
        DailyPaymentsBtn.Click += (_, __) => NavigateToDailyPayments();
        ProgramsBtn.Click += (_, __) => NavigateToPrograms();
        AcademicYearsBtn.Click += (_, __) => NavigateToAcademicYears();
        StatisticsBtn.Click += (_, __) => NavigateToStatistics();
        SettingsBtn.Click += (_, __) => NavigateToDatabaseImport();

        _ = RefreshAcademicYearProgressAsync();
    }

    private void OnAppStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppState.SelectedDatabaseMode) ||
            e.PropertyName == nameof(AppState.SelectedRemoteDatabaseName) ||
            e.PropertyName == nameof(AppState.SelectedLocalDatabaseName) ||
            e.PropertyName == nameof(AppState.HasBothLocalDatabases) ||
            e.PropertyName == nameof(AppState.IsLocalModeEnabled) ||
            e.PropertyName == nameof(AppState.AvailableLocalDatabases))
        {
            RefreshModeOptions();
            RefreshLocalDatabaseOptions();
            SyncTopBarState();
        }

        if (e.PropertyName == nameof(AppState.SelectedAcademicYear) ||
            e.PropertyName == nameof(AppState.DataVersion))
        {
            _ = RefreshAcademicYearProgressAsync();
        }
    }

    private void SyncTopBarState()
    {
        ModeCombo.SelectedItem = _state.SelectedDatabaseMode;
        DbCombo.SelectedValue = _state.SelectedRemoteDatabaseName;
        LocalDbCombo.SelectedValue = _state.SelectedLocalDatabaseName;
        RemoteDbGrid.Visibility = _state.SelectedDatabaseMode == DatabaseMode.Remote
            ? Visibility.Visible
            : Visibility.Collapsed;
        LocalDbGrid.Visibility = _state.SelectedDatabaseMode == DatabaseMode.Local
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (!_state.HasBothLocalDatabases)
        {
            LocalDbGrid.Visibility = Visibility.Collapsed;
        }

        var databaseFeaturesEnabled = _state.IsDatabaseImportEnabled;
        ModeCombo.IsEnabled = databaseFeaturesEnabled;
        DbCombo.IsEnabled = databaseFeaturesEnabled;
        LocalDbCombo.IsEnabled = databaseFeaturesEnabled;
    }

    private void RefreshModeOptions()
    {
        ModeCombo.ItemsSource = _state.IsLocalModeEnabled
            ? new[] { DatabaseMode.Local, DatabaseMode.Remote }
            : new[] { DatabaseMode.Remote };

        if (!_state.IsLocalModeEnabled)
            _state.SelectedDatabaseMode = DatabaseMode.Remote;
    }

    private void RefreshLocalDatabaseOptions()
    {
        LocalDbCombo.ItemsSource = _allLocalDatabases
            .Where(x => _state.AvailableLocalDatabases.Contains(x.Database))
            .ToList();
    }

    private async Task RefreshAcademicYearProgressAsync()
    {
        try
        {
            using var db = _dbFactory.Create();

            var year = _state.SelectedAcademicYear;
            var period = await db.AcademicPeriods
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Name == year);

            if (period is null)
            {
                YearProgressBar.Value = 0;
                YearRevenueSummaryText.Text = "Εισπράξεις έτους: 0 € / 0 €";
                return;
            }

            var enrollments = await db.Enrollments
                .AsNoTracking()
                .Include(e => e.Payments)
                .Where(e => e.AcademicPeriodId == period.AcademicPeriodId)
                .ToListAsync();

            decimal agreementSum = enrollments.Sum(InstallmentPlanHelper.GetEffectiveAgreementTotal);
            decimal paidSum = enrollments.Sum(e => e.DownPayment + PaymentAgreementHelper.SumAgreementPayments(e.Payments));
            var discontinuedRemaining = enrollments
                .Where(e => e.IsStopped)
                .Sum(InstallmentPlanHelper.GetOutstandingAmount);
            var collectibleSum = Math.Max(0m, agreementSum - discontinuedRemaining);

            var progress = collectibleSum <= 0 ? 0d : (double)(paidSum / collectibleSum * 100m);
            if (progress > 100) progress = 100;
            if (progress < 0) progress = 0;

            YearProgressBar.Value = progress;
            var culture = new CultureInfo("el-GR");
            YearRevenueSummaryText.Text = $"Εισπράξεις έτους: {paidSum.ToString("N0", culture)} € / {collectibleSum.ToString("N0", culture)} €";
        }
        catch
        {
            YearProgressBar.Value = 0;
            YearRevenueSummaryText.Text = "Εισπράξεις έτους: μη διαθέσιμα δεδομένα";
        }
    }


    private IReadOnlyList<Button> NavigationButtons => new[]
    {
        StudentsBtn,
        DailyPaymentsBtn,
        ProgramsBtn,
        AcademicYearsBtn,
        StatisticsBtn,
        SettingsBtn
    };

    private void SetActiveNavigationButton(Button activeButton)
    {
        foreach (var button in NavigationButtons)
        {
            button.Tag = ReferenceEquals(button, activeButton) ? "Active" : null;
        }
    }

    private void OpenStartupOptions()
    {
        var win = App.Services.GetRequiredService<Windows.StartupDatabaseOptionsWindow>();
        win.Owner = this;
        win.Initialize(_state.StartupLocalDatabaseName);

        if (win.ShowDialog() != true)
            return;

        _state.SaveStartupLocalDatabase(win.SelectedDatabaseName);
        MessageBox.Show(
            "Η προεπιλεγμένη βάση εκκίνησης αποθηκεύτηκε.",
            "Επιτυχία",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void NavigateToStudents()
    {
        var view = App.Services.GetRequiredService<StudentsView>();
        MainContent.Content = view;
        SetActiveNavigationButton(StudentsBtn);
    }


    private void NavigateToDailyPayments()
    {
        ClearStudentsSearchIfNeeded();
        var view = App.Services.GetRequiredService<DailyPaymentsView>();
        MainContent.Content = view;
        SetActiveNavigationButton(DailyPaymentsBtn);
    }

    private void NavigateToPrograms()
    {
        ClearStudentsSearchIfNeeded();
        var view = App.Services.GetRequiredService<ProgramsView>();
        MainContent.Content = view;
        SetActiveNavigationButton(ProgramsBtn);
    }

    private void NavigateToAcademicYears()
    {
        ClearStudentsSearchIfNeeded();
        var view = App.Services.GetRequiredService<AcademicYearsView>();
        MainContent.Content = view;
        SetActiveNavigationButton(AcademicYearsBtn);
    }

    private void NavigateToStatistics()
    {
        ClearStudentsSearchIfNeeded();
        var view = App.Services.GetRequiredService<StatisticsView>();
        MainContent.Content = view;
        SetActiveNavigationButton(StatisticsBtn);
    }

    private void NavigateToDatabaseImport()
    {
        ClearStudentsSearchIfNeeded();

        if (!_state.IsDatabaseImportEnabled)
        {
            MessageBox.Show(
                "Η εισαγωγή βάσης είναι απενεργοποιημένη. Εγκαταστήστε πρώτα το Tailscale.",
                "Ρυθμίσεις βάσης",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var view = App.Services.GetRequiredService<DatabaseImportView>();
        MainContent.Content = view;
        SetActiveNavigationButton(SettingsBtn);
    }

    public void NavigateToDatabaseImportFromStartup()
    {
        NavigateToDatabaseImport();
    }

    private void ClearStudentsSearchIfNeeded()
    {
        if (MainContent.Content is StudentsView studentsView &&
            studentsView.DataContext is ViewModels.StudentsViewModel vm)
        {
            vm.SearchText = string.Empty;
        }
    }

    private void SettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        // open settings window here
    }

    private void ProgramsBtn_Click(object sender, RoutedEventArgs e)
    {

    }
}
