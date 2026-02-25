using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.Input;
using LanguageSchoolERP.App.Views;
using LanguageSchoolERP.App.Windows;
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
    private readonly IDatabaseCloneService _databaseCloneService;
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
    public IAsyncRelayCommand GenerateEmptyDatabaseCommand { get; }
    public IAsyncRelayCommand WipeDatabaseCommand { get; }

    public DatabaseImportViewModel(
        DatabaseAppSettingsProvider settingsProvider,
        IDatabaseImportService databaseImportService,
        IDatabaseCloneService databaseCloneService,
        AppState appState)
    {
        _settingsProvider = settingsProvider;
        _databaseImportService = databaseImportService;
        _databaseCloneService = databaseCloneService;
        _appState = appState;

        ImportCommand = new AsyncRelayCommand(ImportAsync, CanImport);
        CancelCommand = new AsyncRelayCommand(CancelAsync, () => IsBusy);
        SaveStartupDatabaseCommand = new RelayCommand(SaveStartupDatabase);
        BackupNowCommand = new AsyncRelayCommand(BackupNowAsync, () => !IsBackupRunning);
        GenerateEmptyDatabaseCommand = new AsyncRelayCommand(GenerateEmptyDatabaseAsync, CanGenerateEmptyDatabase);
        WipeDatabaseCommand = new AsyncRelayCommand(WipeDatabaseAsync, CanWipeDatabase);

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

        _appState.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.HasAnyLocalDatabase) ||
                e.PropertyName == nameof(AppState.AvailableLocalDatabases))
            {
                OnPropertyChanged(nameof(CanShowEmptyDatabaseActions));
                GenerateEmptyDatabaseCommand.NotifyCanExecuteChanged();
                WipeDatabaseCommand.NotifyCanExecuteChanged();
            }
        };

        RefreshBackupStatus();
    }

    public bool CanShowEmptyDatabaseActions => !_appState.HasAnyLocalDatabase;

    partial void OnIsBusyChanged(bool value)
    {
        ImportCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        GenerateEmptyDatabaseCommand.NotifyCanExecuteChanged();
        WipeDatabaseCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBackupRunningChanged(bool value)
    {
        BackupNowCommand.NotifyCanExecuteChanged();
        WipeDatabaseCommand.NotifyCanExecuteChanged();
        GenerateEmptyDatabaseCommand.NotifyCanExecuteChanged();
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
        WipeDatabaseCommand.NotifyCanExecuteChanged();
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
                successTitle = "Clone from Backup";

                AppendLog($"Selected school DB: {localDbName}");

                var school = string.Equals(localDbName, "NeaIoniaSchoolERP", StringComparison.OrdinalIgnoreCase)
                    ? School.NeaIonia
                    : School.Filothei;

                await _databaseCloneService.CloneFromLatestBackupAsync(
                    school,
                    new Progress<string>(message =>
                    {
                        AppendLog(message);

                        if (message.Contains("Copying backup", StringComparison.OrdinalIgnoreCase))
                        {
                            ProgressPercent = 15;
                        }
                        else if (message.Contains("Dropping existing", StringComparison.OrdinalIgnoreCase))
                        {
                            ProgressPercent = 35;
                        }
                        else if (message.Contains("Reading logical", StringComparison.OrdinalIgnoreCase))
                        {
                            ProgressPercent = 55;
                        }
                        else if (message.Contains("Restoring database", StringComparison.OrdinalIgnoreCase))
                        {
                            ProgressPercent = 85;
                        }
                        else if (message.Contains("Restore complete", StringComparison.OrdinalIgnoreCase))
                        {
                            ProgressPercent = 100;
                        }

                        ProgressValue = ProgressPercent;
                    }),
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

            try
            {
                await RefreshLocalDatabaseAvailabilityAsync(settings.Local.Server, _cts.Token);
            }
            catch (Exception ex)
            {
                AppendLog($"WARNING: Could not refresh local DB availability. {ex.Message}");
            }

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

    private bool CanGenerateEmptyDatabase()
    {
        return !IsBusy && !IsBackupRunning && !_appState.HasAnyLocalDatabase;
    }

    private bool CanWipeDatabase()
    {
        return !IsBusy
            && !IsBackupRunning
            && _appState.AvailableLocalDatabases.Contains(SelectedBackupDatabaseName);
    }

    private async Task GenerateEmptyDatabaseAsync()
    {
        var result = MessageBox.Show(
            $"Θα δημιουργηθεί κενή βάση για το branch '{SelectedBackupDatabaseName}'.\n\nΣυνέχεια;",
            "Δημιουργία κενής βάσης",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        IsBusy = true;

        try
        {
            var settings = _settingsProvider.Settings;
            var targetConnectionString = ConnectionStringHelpers.ReplaceDatabase(settings.Local.ConnectionString, SelectedBackupDatabaseName);
            var options = new DbContextOptionsBuilder<LanguageSchoolERP.Data.SchoolDbContext>()
                .UseSqlServer(targetConnectionString)
                .Options;

            await using var db = new LanguageSchoolERP.Data.SchoolDbContext(options);
            await db.Database.MigrateAsync();

            await RefreshLocalDatabaseAvailabilityAsync(settings.Local.Server, CancellationToken.None);
            _appState.SelectedLocalDatabaseName = SelectedBackupDatabaseName;
            _appState.SelectedDatabaseMode = DatabaseMode.Local;
            _appState.NotifyDataChanged();

            GenerateEmptyDatabaseCommand.NotifyCanExecuteChanged();
            WipeDatabaseCommand.NotifyCanExecuteChanged();

            MessageBox.Show(
                "Η κενή βάση δημιουργήθηκε επιτυχώς.",
                "Δημιουργία κενής βάσης",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Η δημιουργία της βάσης απέτυχε. {ex.Message}",
                "Δημιουργία κενής βάσης",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task WipeDatabaseAsync()
    {
        var owner = Application.Current?.MainWindow;
        var confirmationPhrase = $"WIPE {SelectedBackupDatabaseName}";
        var confirmationWindow = new DestructiveActionConfirmationWindow(
            "Ολική εκκαθάριση βάσης",
            $"Η ενέργεια θα διαγράψει όλα τα δεδομένα από τη βάση '{SelectedBackupDatabaseName}' και δεν αναιρείται.\n" +
            "Ο πίνακας migrations (__EFMigrationsHistory) θα παραμείνει ανέπαφος.",
            confirmationPhrase)
        {
            Owner = owner
        };

        if (confirmationWindow.ShowDialog() != true)
            return;

        IsBusy = true;

        try
        {
            await WipeAllDataExceptMigrationsAsync(SelectedBackupDatabaseName);
            _appState.NotifyDataChanged();

            MessageBox.Show(
                "Η βάση εκκαθαρίστηκε επιτυχώς.",
                "Ολική εκκαθάριση βάσης",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Η εκκαθάριση απέτυχε. {ex.Message}",
                "Ολική εκκαθάριση βάσης",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task WipeAllDataExceptMigrationsAsync(string databaseName)
    {
        var localConnection = ConnectionStringHelpers.ReplaceDatabase(_settingsProvider.Settings.Local.ConnectionString, databaseName);

        await using var connection = new SqlConnection(localConnection);
        await connection.OpenAsync();

        const string sql = """
DECLARE @sql NVARCHAR(MAX) = N'';

SELECT @sql += N'ALTER TABLE [' + s.name + N'].[' + t.name + N'] NOCHECK CONSTRAINT ALL;' + CHAR(10)
FROM sys.tables t
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.is_ms_shipped = 0
  AND t.name <> N'__EFMigrationsHistory';

SELECT @sql += N'DELETE FROM [' + s.name + N'].[' + t.name + N'];' + CHAR(10)
FROM sys.tables t
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.is_ms_shipped = 0
  AND t.name <> N'__EFMigrationsHistory';

SELECT @sql += N'ALTER TABLE [' + s.name + N'].[' + t.name + N'] WITH CHECK CHECK CONSTRAINT ALL;' + CHAR(10)
FROM sys.tables t
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.is_ms_shipped = 0
  AND t.name <> N'__EFMigrationsHistory';

EXEC sp_executesql @sql;
""";

        await using var command = new SqlCommand(sql, connection)
        {
            CommandTimeout = 0
        };

        await command.ExecuteNonQueryAsync();
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

    private async Task RefreshLocalDatabaseAvailabilityAsync(string server, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(server))
            return;

        var connectionString = DatabaseAppSettingsProvider.BuildTrustedConnectionString(server, "master");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        const string sql = """
SELECT [name]
FROM sys.databases
WHERE [name] IN (N'FilotheiSchoolERP', N'NeaIoniaSchoolERP');
""";

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);

        var hasFilothei = false;
        var hasNeaIonia = false;

        while (await reader.ReadAsync(ct))
        {
            var name = reader.GetString(0);
            if (string.Equals(name, "FilotheiSchoolERP", StringComparison.OrdinalIgnoreCase))
                hasFilothei = true;
            else if (string.Equals(name, "NeaIoniaSchoolERP", StringComparison.OrdinalIgnoreCase))
                hasNeaIonia = true;
        }

        _appState.UpdateLocalDatabaseAvailability(hasFilothei, hasNeaIonia);
    }

    private bool ConfirmImport()
    {
        if (SelectedImportSource != DatabaseImportSource.ExcelFiles)
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
