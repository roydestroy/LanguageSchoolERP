using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.Input;
using LanguageSchoolERP.App.Views;
using LanguageSchoolERP.Services;

namespace LanguageSchoolERP.App.ViewModels;

public enum DatabaseImportSource
{
    RemoteDatabase,
    BackupFile
}

public partial class DatabaseImportViewModel : ObservableObject
{
    private readonly DatabaseAppSettingsProvider _settingsProvider;
    private readonly IDatabaseImportService _databaseImportService;
    private readonly AppState _appState;
    private readonly StringBuilder _logBuilder = new();
    private CancellationTokenSource? _cts;

    public ObservableCollection<RemoteDatabaseOption> RemoteDatabases { get; } = [];
    public ObservableCollection<RemoteDatabaseOption> LocalRestoreDatabases { get; } = [];

    [ObservableProperty] private DatabaseImportSource selectedImportSource = DatabaseImportSource.RemoteDatabase;
    [ObservableProperty] private RemoteDatabaseOption? selectedRemoteDatabaseOption;
    [ObservableProperty] private RemoteDatabaseOption? selectedLocalRestoreDatabaseOption;
    [ObservableProperty] private string backupFilePath = string.Empty;
    [ObservableProperty] private bool wipeLocalFirst = true;
    [ObservableProperty] private string log = string.Empty;
    [ObservableProperty] private double progressValue;
    [ObservableProperty] private int progressPercent;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string startupLocalDatabaseName = "FilotheiSchoolERP";
    [ObservableProperty] private bool startupFilotheiSelected;
    [ObservableProperty] private bool startupNeaIoniaSelected;
    [ObservableProperty] private string lastBackupText = "Ποτέ";
    [ObservableProperty] private string lastBackupErrorText = string.Empty;
    [ObservableProperty] private bool isBackupRunning;


    public bool IsRemoteImportSelected
    {
        get => SelectedImportSource == DatabaseImportSource.RemoteDatabase;
        set
        {
            if (value)
                SelectedImportSource = DatabaseImportSource.RemoteDatabase;
        }
    }

    public bool IsBackupImportSelected
    {
        get => SelectedImportSource == DatabaseImportSource.BackupFile;
        set
        {
            if (value)
                SelectedImportSource = DatabaseImportSource.BackupFile;
        }
    }

    public IAsyncRelayCommand ImportCommand { get; }
    public IAsyncRelayCommand CancelCommand { get; }
    public IRelayCommand SaveStartupDatabaseCommand { get; }
    public IAsyncRelayCommand BackupNowCommand { get; }

    public DatabaseImportViewModel(
        DatabaseAppSettingsProvider settingsProvider,
        IDatabaseImportService databaseImportService,
        AppState appState)
    {
        _settingsProvider = settingsProvider;
        _databaseImportService = databaseImportService;
        _appState = appState;

        ImportCommand = new AsyncRelayCommand(ImportAsync, CanImport);
        CancelCommand = new AsyncRelayCommand(CancelAsync, () => IsBusy);
        SaveStartupDatabaseCommand = new RelayCommand(SaveStartupDatabase);
        BackupNowCommand = new AsyncRelayCommand(BackupNowAsync, () => !IsBackupRunning);

        StartupLocalDatabaseName = string.IsNullOrWhiteSpace(_appState.StartupLocalDatabaseName)
            ? "FilotheiSchoolERP"
            : _appState.StartupLocalDatabaseName;

        foreach (var option in _settingsProvider.RemoteDatabases)
            RemoteDatabases.Add(option);

        LocalRestoreDatabases.Add(new RemoteDatabaseOption { Key = "Filothei", Database = "FilotheiSchoolERP" });
        LocalRestoreDatabases.Add(new RemoteDatabaseOption { Key = "Nea Ionia", Database = "NeaIoniaSchoolERP" });

        SelectedRemoteDatabaseOption = RemoteDatabases.FirstOrDefault(); // now safe
        SelectedLocalRestoreDatabaseOption = LocalRestoreDatabases.FirstOrDefault(x => string.Equals(x.Database, StartupLocalDatabaseName, StringComparison.OrdinalIgnoreCase));

        RefreshBackupStatus();
    }

    partial void OnIsBusyChanged(bool value)
    {
        ImportCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBackupRunningChanged(bool value)
    {
        BackupNowCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedRemoteDatabaseOptionChanged(RemoteDatabaseOption? value)
    {
        ImportCommand?.NotifyCanExecuteChanged();
    }




    partial void OnSelectedImportSourceChanged(DatabaseImportSource value)
    {
        OnPropertyChanged(nameof(IsRemoteImportSelected));
        OnPropertyChanged(nameof(IsBackupImportSelected));
        ImportCommand?.NotifyCanExecuteChanged();
    }

    partial void OnBackupFilePathChanged(string value)
    {
        ImportCommand?.NotifyCanExecuteChanged();
    }

    partial void OnSelectedLocalRestoreDatabaseOptionChanged(RemoteDatabaseOption? value)
    {
        ImportCommand?.NotifyCanExecuteChanged();
    }

    partial void OnStartupLocalDatabaseNameChanged(string value)
    {
        StartupFilotheiSelected = value == "FilotheiSchoolERP";
        StartupNeaIoniaSelected = value == "NeaIoniaSchoolERP";

        if (SelectedLocalRestoreDatabaseOption is null)
        {
            SelectedLocalRestoreDatabaseOption = LocalRestoreDatabases.FirstOrDefault(x => string.Equals(x.Database, value, StringComparison.OrdinalIgnoreCase));
        }
    }

    partial void OnStartupFilotheiSelectedChanged(bool value)
    {
        if (value)
            StartupLocalDatabaseName = "FilotheiSchoolERP";
    }

    partial void OnStartupNeaIoniaSelectedChanged(bool value)
    {
        if (value)
            StartupLocalDatabaseName = "NeaIoniaSchoolERP";
    }

    private void SaveStartupDatabase()
    {
        _appState.SaveStartupLocalDatabase(StartupLocalDatabaseName);
        MessageBox.Show(
            "Η προεπιλεγμένη βάση εκκίνησης αποθηκεύτηκε.",
            "Ρυθμίσεις",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private bool CanImport()
    {
        if (IsBusy)
            return false;

        return SelectedImportSource switch
        {
            DatabaseImportSource.RemoteDatabase => SelectedRemoteDatabaseOption is not null,
            DatabaseImportSource.BackupFile => SelectedLocalRestoreDatabaseOption is not null && !string.IsNullOrWhiteSpace(BackupFilePath),
            _ => false
        };
    }

    private Task CancelAsync()
    {
        _cts?.Cancel();
        AppendLog("Cancellation requested...");
        return Task.CompletedTask;
    }

    private async Task ImportAsync()
    {
        if (!ConfirmImport())
        {
            return;
        }

        _cts = new CancellationTokenSource();
        IsBusy = true;
        ProgressValue = 0;
        ProgressPercent = 0;
        _logBuilder.Clear();
        Log = string.Empty;

        try
        {
            var settings = _settingsProvider.Settings;

            var progress = new Progress<ImportProgress>(p =>
            {
                ProgressPercent = p.Percent;
                ProgressValue = p.Percent;
                AppendLog($"[{p.Percent}%] {p.Message}");
            });

            string localDbName;
            string successTitle;

            if (SelectedImportSource == DatabaseImportSource.BackupFile)
            {
                if (SelectedLocalRestoreDatabaseOption is null)
                    throw new InvalidOperationException("Local restore database is required.");

                if (string.IsNullOrWhiteSpace(BackupFilePath))
                    throw new InvalidOperationException("Backup file path is required.");

                localDbName = SelectedLocalRestoreDatabaseOption.Database;
                successTitle = "Import from Backup";

                var localConnection = ConnectionStringHelpers.ReplaceDatabase(settings.Local.ConnectionString, localDbName);

                AppendLog($"Backup source: {BackupFilePath}");
                AppendLog($"Local target: {localDbName}");

                await _databaseImportService.ImportFromBackupAsync(
                    BackupFilePath,
                    localConnection,
                    progress,
                    _cts.Token);
            }
            else
            {
                if (SelectedRemoteDatabaseOption is null)
                    throw new InvalidOperationException("Remote database source is required.");

                var selectedRemoteDbName = SelectedRemoteDatabaseOption.Database;
                localDbName = ConnectionStringHelpers.EnsureLocalDatabaseName(selectedRemoteDbName);
                var remoteDbName = ConnectionStringHelpers.EnsureRemoteDatabaseName(localDbName);
                successTitle = "Import from Remote";

                var localConnection = ConnectionStringHelpers.ReplaceDatabase(settings.Local.ConnectionString, localDbName);
                var remoteConnection = ConnectionStringHelpers.ReplaceDatabase(settings.Remote.ConnectionString, remoteDbName);

                AppendLog($"Remote source: {remoteDbName}");
                AppendLog($"Local target: {localDbName}");

                await _databaseImportService.ImportFromRemoteAsync(
                    remoteConnection,
                    localConnection,
                    WipeLocalFirst,
                    progress,
                    _cts.Token);
            }

            _appState.SelectedLocalDatabaseName = localDbName;
            _appState.SelectedDatabaseMode = DatabaseMode.Local;
            _appState.NotifyDataChanged();

            MessageBox.Show(
                "Η εισαγωγή ολοκληρώθηκε επιτυχώς.",
                successTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            AppendLog("Import cancelled by user.");
            MessageBox.Show(
                "Η εισαγωγή ακυρώθηκε.",
                "Import",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppendLog($"ERROR: {ex}");
            MessageBox.Show(
                "Η εισαγωγή απέτυχε. Ελέγξτε το log για λεπτομέρειες.",
                "Import",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void BrowseBackupFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "SQL Backup (*.bak)|*.bak|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
            Title = "Επιλογή backup αρχείου"
        };

        if (dialog.ShowDialog() == true)
        {
            BackupFilePath = dialog.FileName;
        }
    }

    private async Task BackupNowAsync()
    {
        IsBackupRunning = true;

        try
        {
            var exitCode = await BackupTaskRunner.RunAsync(force: true);
            RefreshBackupStatus();

            if (exitCode == 0)
            {
                MessageBox.Show(
                    "Το backup ολοκληρώθηκε επιτυχώς.",
                    "Backups",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var status = BackupStatusStore.TryRead();
            var error = string.IsNullOrWhiteSpace(status?.LastError)
                ? "Το backup απέτυχε. Ελέγξτε δικαιώματα πρόσβασης και ρυθμίσεις backup."
                : $"Το backup απέτυχε. {status.LastError}";

            MessageBox.Show(
                error,
                "Backups",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show(
                "Δεν υπάρχουν επαρκή δικαιώματα για εκτέλεση backup από τον τρέχοντα χρήστη. Δοκιμάστε εκτέλεση ως διαχειριστής.",
                "Backups",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Δεν ήταν δυνατή η εκτέλεση backup. {ex.Message}",
                "Backups",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsBackupRunning = false;
            RefreshBackupStatus();
        }
    }

    private void RefreshBackupStatus()
    {
        var status = BackupStatusStore.TryRead();
        if (status?.LastSuccessUtc is DateTime successUtc)
        {
            var local = successUtc.ToLocalTime();
            var result = string.IsNullOrWhiteSpace(status.LastResult) ? "Unknown" : status.LastResult;
            LastBackupText = $"{local.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture)} ({result})";
        }
        else if (status?.LastAttemptUtc is DateTime attemptUtc)
        {
            var local = attemptUtc.ToLocalTime();
            var result = string.IsNullOrWhiteSpace(status.LastResult) ? "Unknown" : status.LastResult;
            LastBackupText = $"{local.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture)} ({result})";
        }
        else
        {
            LastBackupText = "Ποτέ";
        }

        LastBackupErrorText = status is not null && string.Equals(status.LastResult, "Failed", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(status.LastError)
            ? status.LastError
            : string.Empty;
    }

    private bool ConfirmImport()
    {
        if (SelectedImportSource == DatabaseImportSource.BackupFile || WipeLocalFirst)
        {
            var dialog = new ConfirmImportDialog
            {
                Owner = Application.Current?.MainWindow
            };

            return dialog.ShowDialog() == true;
        }

        var result = MessageBox.Show(
            "Θα γίνει εισαγωγή δεδομένων στην τοπική βάση.\n\nΣυνέχεια;",
            "Επιβεβαίωση Import",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        return result == MessageBoxResult.Yes;
    }

    private void AppendLog(string message)
    {
        _logBuilder.AppendLine($"{DateTime.Now:HH:mm:ss} - {message}");
        Log = _logBuilder.ToString();
    }
}
