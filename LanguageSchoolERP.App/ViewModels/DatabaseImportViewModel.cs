using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanguageSchoolERP.App.Views;
using LanguageSchoolERP.Services;

namespace LanguageSchoolERP.App.ViewModels;

public partial class DatabaseImportViewModel : ObservableObject
{
    private readonly DatabaseAppSettingsProvider _settingsProvider;
    private readonly IDatabaseImportService _databaseImportService;
    private readonly AppState _appState;
    private readonly StringBuilder _logBuilder = new();
    private CancellationTokenSource? _cts;

    public ObservableCollection<RemoteDatabaseOption> RemoteDatabases { get; } = [];

    [ObservableProperty] private RemoteDatabaseOption? selectedRemoteDatabaseOption;
    [ObservableProperty] private bool wipeLocalFirst = true;
    [ObservableProperty] private string log = string.Empty;
    [ObservableProperty] private double progressValue;
    [ObservableProperty] private int progressPercent;
    [ObservableProperty] private bool isBusy;

    public IAsyncRelayCommand ImportCommand { get; }
    public IAsyncRelayCommand CancelCommand { get; }

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

        foreach (var option in _settingsProvider.RemoteDatabases)
            RemoteDatabases.Add(option);

        SelectedRemoteDatabaseOption = RemoteDatabases.FirstOrDefault(); // now safe
    }

    partial void OnIsBusyChanged(bool value)
    {
        ImportCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedRemoteDatabaseOptionChanged(RemoteDatabaseOption? value)
    {
        ImportCommand?.NotifyCanExecuteChanged();
    }


    private bool CanImport() => !IsBusy && SelectedRemoteDatabaseOption is not null;

    private Task CancelAsync()
    {
        _cts?.Cancel();
        AppendLog("Cancellation requested...");
        return Task.CompletedTask;
    }

    private async Task ImportAsync()
    {
        if (SelectedRemoteDatabaseOption is null)
        {
            return;
        }

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
            var selectedRemoteDbName = SelectedRemoteDatabaseOption.Database;
            var localDbName = ConnectionStringHelpers.EnsureLocalDatabaseName(selectedRemoteDbName);
            var remoteDbName = ConnectionStringHelpers.EnsureRemoteDatabaseName(localDbName);

            var localConnection = ConnectionStringHelpers.ReplaceDatabase(settings.Local.ConnectionString, localDbName);
            var remoteConnection = ConnectionStringHelpers.ReplaceDatabase(settings.Remote.ConnectionString, remoteDbName);

            AppendLog($"Remote source: {remoteDbName}");
            AppendLog($"Local target: {localDbName}");

            var progress = new Progress<ImportProgress>(p =>
            {
                ProgressPercent = p.Percent;
                ProgressValue = p.Percent;
                AppendLog($"[{p.Percent}%] {p.Message}");
            });

            await _databaseImportService.ImportFromRemoteAsync(
                remoteConnection,
                localConnection,
                WipeLocalFirst,
                progress,
                _cts.Token);

            _appState.SelectedLocalDatabaseName = localDbName;
            _appState.SelectedDatabaseMode = DatabaseMode.Local;
            _appState.NotifyDataChanged();

            MessageBox.Show(
                "Η εισαγωγή ολοκληρώθηκε επιτυχώς.",
                "Import from Remote",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            AppendLog("Import cancelled by user.");
            MessageBox.Show(
                "Η εισαγωγή ακυρώθηκε.",
                "Import from Remote",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppendLog($"ERROR: {ex}");
            MessageBox.Show(
                "Η εισαγωγή απέτυχε. Ελέγξτε το log για λεπτομέρειες.",
                "Import from Remote",
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

    private bool ConfirmImport()
    {
        if (WipeLocalFirst)
        {
            var dialog = new ConfirmImportDialog
            {
                Owner = Application.Current?.MainWindow
            };

            return dialog.ShowDialog() == true;
        }

        var result = MessageBox.Show(
            "Θα γίνει εισαγωγή δεδομένων από remote βάση στην τοπική βάση.\n\nΣυνέχεια;",
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
