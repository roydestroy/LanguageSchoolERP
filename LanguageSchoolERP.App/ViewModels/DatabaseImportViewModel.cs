using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
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
    BackupFile,
    ExcelFiles
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
    [ObservableProperty] private string selectedExcelFilesText = string.Empty;
    [ObservableProperty] private string selectedExcelTargetDatabaseName = "FilotheiSchoolERP";
    [ObservableProperty] private bool excelTargetFilotheiSelected = true;
    [ObservableProperty] private bool excelTargetNeaIoniaSelected;
    [ObservableProperty] private bool excelDryRunMode;
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
    [ObservableProperty] private string selectedBackupDatabaseName = "FilotheiSchoolERP";
    [ObservableProperty] private bool backupFilotheiSelected = true;
    [ObservableProperty] private bool backupNeaIoniaSelected;


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

    public ObservableCollection<string> ExcelFilePaths { get; } = [];

    public bool IsExcelImportSelected
    {
        get => SelectedImportSource == DatabaseImportSource.ExcelFiles;
        set
        {
            if (value)
                SelectedImportSource = DatabaseImportSource.ExcelFiles;
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

        SelectedBackupDatabaseName = string.Equals(_appState.SelectedLocalDatabaseName, "NeaIoniaSchoolERP", StringComparison.OrdinalIgnoreCase)
            ? "NeaIoniaSchoolERP"
            : "FilotheiSchoolERP";

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
        OnPropertyChanged(nameof(IsExcelImportSelected));
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

    partial void OnSelectedExcelFilesTextChanged(string value)
    {
        ImportCommand?.NotifyCanExecuteChanged();
    }

    partial void OnSelectedExcelTargetDatabaseNameChanged(string value)
    {
        ExcelTargetFilotheiSelected = string.Equals(value, "FilotheiSchoolERP", StringComparison.OrdinalIgnoreCase);
        ExcelTargetNeaIoniaSelected = string.Equals(value, "NeaIoniaSchoolERP", StringComparison.OrdinalIgnoreCase);
        ImportCommand?.NotifyCanExecuteChanged();
    }

    partial void OnExcelTargetFilotheiSelectedChanged(bool value)
    {
        if (value)
            SelectedExcelTargetDatabaseName = "FilotheiSchoolERP";
    }

    partial void OnExcelTargetNeaIoniaSelectedChanged(bool value)
    {
        if (value)
            SelectedExcelTargetDatabaseName = "NeaIoniaSchoolERP";
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

    partial void OnSelectedBackupDatabaseNameChanged(string value)
    {
        BackupFilotheiSelected = string.Equals(value, "FilotheiSchoolERP", StringComparison.OrdinalIgnoreCase);
        BackupNeaIoniaSelected = string.Equals(value, "NeaIoniaSchoolERP", StringComparison.OrdinalIgnoreCase);
        RefreshBackupStatus();
    }

    partial void OnBackupFilotheiSelectedChanged(bool value)
    {
        if (value)
            SelectedBackupDatabaseName = "FilotheiSchoolERP";
    }

    partial void OnBackupNeaIoniaSelectedChanged(bool value)
    {
        if (value)
            SelectedBackupDatabaseName = "NeaIoniaSchoolERP";
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
            DatabaseImportSource.ExcelFiles => ExcelFilePaths.Count > 0 && !string.IsNullOrWhiteSpace(SelectedExcelTargetDatabaseName),
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
            else if (SelectedImportSource == DatabaseImportSource.ExcelFiles)
            {
                if (ExcelFilePaths.Count == 0)
                    throw new InvalidOperationException("At least one Excel file is required.");

                localDbName = SelectedExcelTargetDatabaseName;
                successTitle = "Import from Excel";

                var localConnection = ConnectionStringHelpers.ReplaceDatabase(settings.Local.ConnectionString, localDbName);

                AppendLog($"Excel source files: {string.Join(", ", ExcelFilePaths)}");
                AppendLog($"Local target: {localDbName}");
                AppendLog($"Dry run: {ExcelDryRunMode}");

                await _databaseImportService.ImportFromExcelAsync(
                    ExcelFilePaths,
                    localConnection,
                    ExcelDryRunMode,
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

                var connectivity = await RemoteConnectivityDiagnostics.CheckRemoteSqlAsync(remoteConnection, _cts.Token);
                if (!connectivity.IsSuccess)
                {
                    var details = string.IsNullOrWhiteSpace(connectivity.Details) ? string.Empty : $"\n\n{connectivity.Details}";
                    AppendLog($"Connectivity check failed: {connectivity.UserMessage}. {connectivity.Details}");
                    MessageBox.Show(
                        $"{connectivity.UserMessage}{details}",
                        "Import",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                await _databaseImportService.ImportFromRemoteAsync(
                    remoteConnection,
                    localConnection,
                    WipeLocalFirst,
                    progress,
                    _cts.Token);
            }

            if (_cts?.IsCancellationRequested == true)
            {
                AppendLog("Import cancelled by user.");
                MessageBox.Show(
                    "Η εισαγωγή ακυρώθηκε.",
                    "Import",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
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


    [RelayCommand]
    private void BrowseExcelFiles()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Excel Files (*.xlsx;*.xlsm)|*.xlsx;*.xlsm",
            CheckFileExists = true,
            Multiselect = true,
            Title = "Επιλογή αρχείων Excel"
        };

        if (dialog.ShowDialog() == true)
        {
            ExcelFilePaths.Clear();
            foreach (var fileName in dialog.FileNames)
                ExcelFilePaths.Add(fileName);

            SelectedExcelFilesText = string.Join("; ", dialog.FileNames);
        }
    }

    private async Task BackupNowAsync()
    {
        IsBackupRunning = true;

        try
        {
            var exitCode = await BackupTaskRunner.RunAsync(force: true, localDatabaseName: SelectedBackupDatabaseName);
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
        var selectedDbName = SelectedBackupDatabaseName;
        var latestBackup = GetLatestBackupTimestamp(selectedDbName);

        LastBackupText = latestBackup.HasValue
            ? latestBackup.Value.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture)
            : "Ποτέ";

        var status = BackupStatusStore.TryRead();
        LastBackupErrorText = status is not null
            && string.Equals(status.LastResult, "Failed", StringComparison.OrdinalIgnoreCase)
            && string.Equals(status.LastDatabaseName, selectedDbName, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(status.LastError)
                ? status.LastError
                : string.Empty;
    }

    private DateTime? GetLatestBackupTimestamp(string databaseName)
    {
        var backupDirectory = _settingsProvider.Settings.Backup.LocalBackupDir;
        if (string.IsNullOrWhiteSpace(backupDirectory) || !Directory.Exists(backupDirectory))
            return null;

        try
        {
            return Directory
                .GetFiles(backupDirectory, $"{databaseName}_*.bak")
                .Select(path => (DateTime?)new FileInfo(path).LastWriteTime)
                .OrderByDescending(dt => dt)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private bool ConfirmImport()
    {
        if (SelectedImportSource == DatabaseImportSource.RemoteDatabase && WipeLocalFirst)
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
