using System;
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
    private const string TailscaleDownloadUrl = "https://tailscale.com/download";
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

        _ = LoadAcademicYearsAsync();

        ModeCombo.SelectionChanged += async (_, __) =>
        {
            if (ModeCombo.SelectedItem is DatabaseMode mode)
            {
                if (mode == DatabaseMode.Local && !_state.IsLocalModeEnabled)
                {
                    _state.SelectedDatabaseMode = _state.IsRemoteModeEnabled ? DatabaseMode.Remote : _state.SelectedDatabaseMode;
                    ModeCombo.SelectedItem = _state.SelectedDatabaseMode;
                    MessageBox.Show(
                        "Η local λειτουργία δεν είναι διαθέσιμη μέχρι να γίνει εισαγωγή τοπικής βάσης.",
                        "Τοπική βάση",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                if (mode == DatabaseMode.Remote && !_state.IsRemoteModeEnabled)
                {
                    _state.SelectedDatabaseMode = _state.IsLocalModeEnabled ? DatabaseMode.Local : _state.SelectedDatabaseMode;
                    ModeCombo.SelectedItem = _state.SelectedDatabaseMode;

                    if (!_state.IsTailscaleInstalled)
                    {
                        ShowWarningWithDownload(
                            "Δεν βρέθηκε εγκατεστημένο το Tailscale.\nΕγκαταστήστε το Tailscale και συνδεθείτε στον λογαριασμό σας για να χρησιμοποιήσετε απομακρυσμένη βάση.",
                            "Tailscale",
                            TailscaleDownloadUrl,
                            "Λήψη Tailscale");
                    }
                    else
                    {
                        MessageBox.Show(
                            "Η remote λειτουργία δεν είναι διαθέσιμη. Ελέγξτε το Tailscale και τη σύνδεσή σας.",
                            "Remote βάση",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
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
        ContactsExportBtn.Click += (_, __) => NavigateToStudentContactsExport();
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
            e.PropertyName == nameof(AppState.IsRemoteModeEnabled) ||
            e.PropertyName == nameof(AppState.IsDatabaseImportEnabled) ||
            e.PropertyName == nameof(AppState.AvailableLocalDatabases))
        {
            RefreshModeOptions();
            RefreshLocalDatabaseOptions();
            SyncTopBarState();
            _ = LoadAcademicYearsAsync();
        }

        if (e.PropertyName == nameof(AppState.SelectedAcademicYear) ||
            e.PropertyName == nameof(AppState.DataVersion))
        {
            _ = LoadAcademicYearsAsync();
            _ = RefreshAcademicYearProgressAsync();
        }
    }

    private void SyncTopBarState()
    {
        ModeCombo.SelectedItem = _state.SelectedDatabaseMode;
        DbCombo.SelectedValue = _state.SelectedRemoteDatabaseName;
        LocalDbCombo.SelectedValue = _state.SelectedLocalDatabaseName;
        RemoteDbGrid.Visibility = _state.IsRemoteModeEnabled && _state.SelectedDatabaseMode == DatabaseMode.Remote
            ? Visibility.Visible
            : Visibility.Collapsed;
        LocalDbGrid.Visibility = _state.SelectedDatabaseMode == DatabaseMode.Local
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (!_state.HasBothLocalDatabases)
        {
            LocalDbGrid.Visibility = Visibility.Collapsed;
        }

        if (!_state.IsRemoteModeEnabled)
        {
            RemoteDbGrid.Visibility = Visibility.Collapsed;
        }

        var hasAnyMode = _state.IsLocalModeEnabled || _state.IsRemoteModeEnabled;
        ModeGrid.Visibility = hasAnyMode ? Visibility.Visible : Visibility.Collapsed;

        ModeCombo.IsEnabled = hasAnyMode;
        DbCombo.IsEnabled = _state.IsRemoteModeEnabled;
        LocalDbCombo.IsEnabled = _state.IsLocalModeEnabled;
    }

    private void RefreshModeOptions()
    {
        if (_state.IsLocalModeEnabled)
        {
            ModeCombo.ItemsSource = new[] { DatabaseMode.Local, DatabaseMode.Remote };
        }
        else if (_state.IsRemoteModeEnabled)
        {
            ModeCombo.ItemsSource = new[] { DatabaseMode.Remote };
            _state.SelectedDatabaseMode = DatabaseMode.Remote;
        }
        else
        {
            ModeCombo.ItemsSource = Array.Empty<DatabaseMode>();
        }
    }

    private void RefreshLocalDatabaseOptions()
    {
        LocalDbCombo.ItemsSource = _allLocalDatabases
            .Where(x => _state.AvailableLocalDatabases.Contains(x.Database))
            .ToList();
    }


    private async Task LoadAcademicYearsAsync()
    {
        var previousYears = (YearCombo.ItemsSource as IEnumerable<string>)?.ToList() ?? [];
        var previousSelected = YearCombo.SelectedItem as string;

        try
        {
            using var db = _dbFactory.Create();

            var years = await db.AcademicPeriods
                .AsNoTracking()
                .OrderByDescending(p => p.Name)
                .Select(p => p.Name)
                .ToListAsync();

            if (years.Count == 0)
            {
                if (previousYears.Count > 0)
                {
                    YearCombo.ItemsSource = previousYears;
                    YearCombo.SelectedItem = previousSelected;
                }
                else
                {
                    YearCombo.ItemsSource = Array.Empty<string>();
                    YearCombo.SelectedItem = null;
                }

                return;
            }

            YearCombo.ItemsSource = years;

            if (!years.Contains(_state.SelectedAcademicYear))
            {
                _state.SelectedAcademicYear = years[0];
            }

            YearCombo.SelectedItem = _state.SelectedAcademicYear;
        }
        catch
        {
            if (previousYears.Count > 0)
            {
                YearCombo.ItemsSource = previousYears;
                YearCombo.SelectedItem = previousSelected;
            }
            else
            {
                YearCombo.ItemsSource = Array.Empty<string>();
                YearCombo.SelectedItem = null;
            }
        }
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
        ContactsExportBtn,
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


    private void NavigateToStudentContactsExport()
    {
        ClearStudentsSearchIfNeeded();
        var view = App.Services.GetRequiredService<StudentContactsExportView>();
        MainContent.Content = view;
        SetActiveNavigationButton(ContactsExportBtn);
    }

    private void NavigateToPrograms()
    {
        ClearStudentsSearchIfNeeded();
        var view = App.Services.GetRequiredService<ProgramsView>();
        MainContent.Content = view;
        SetActiveNavigationButton(SettingsBtn);
    }

    private void NavigateToAcademicYears()
    {
        ClearStudentsSearchIfNeeded();
        var view = App.Services.GetRequiredService<AcademicYearsView>();
        MainContent.Content = view;
        SetActiveNavigationButton(SettingsBtn);
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

        var view = App.Services.GetRequiredService<DatabaseImportView>();
        MainContent.Content = view;
        SetActiveNavigationButton(SettingsBtn);
    }

    private static void ShowWarningWithDownload(string message, string caption, string downloadUrl, string buttonText)
    {
        var result = MessageBox.Show(
            $"{message}\n\nΘέλετε να ανοίξει η σελίδα λήψης;",
            caption,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = downloadUrl,
                UseShellExecute = true
            });
        }
        catch
        {
            MessageBox.Show(
                "Δεν ήταν δυνατό να ανοίξει ο browser για λήψη.",
                caption,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    public void NavigateToDatabaseImportFromStartup()
    {
        NavigateToDatabaseImport();
    }

    public void NavigateToProgramsFromSettings()
    {
        NavigateToPrograms();
    }

    public void NavigateToAcademicYearsFromSettings()
    {
        NavigateToAcademicYears();
    }

    private void ClearStudentsSearchIfNeeded()
    {
        if (MainContent.Content is StudentsView studentsView &&
            studentsView.DataContext is ViewModels.StudentsViewModel vm)
        {
            vm.SearchText = string.Empty;
        }
    }

    private void SettingsBtn_Click(object sender, RoutedEventArgs e) { }

    private void ProgramsBtn_Click(object sender, RoutedEventArgs e) { }
}
