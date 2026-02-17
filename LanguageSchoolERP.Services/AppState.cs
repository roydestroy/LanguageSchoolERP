using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LanguageSchoolERP.Services;

public class AppState : INotifyPropertyChanged
{
    private readonly DatabaseAppSettingsProvider _settingsProvider;

    private DatabaseMode _selectedDatabaseMode = DatabaseMode.Local;
    private string _selectedLocalDatabaseName = "FilotheiSchoolERP";
    private string _selectedRemoteDatabaseName = "FilotheiSchoolERP_View";
    private string _selectedDatabaseName = "FilotheiSchoolERP";
    private string _selectedAcademicYear = "2025-2026";
    private long _dataVersion;

    public AppState(DatabaseAppSettingsProvider settingsProvider)
    {
        _settingsProvider = settingsProvider;

        _selectedRemoteDatabaseName = settingsProvider.RemoteDatabases.FirstOrDefault()?.Database
            ?? "FilotheiSchoolERP_View";

        _selectedLocalDatabaseName = settingsProvider.Settings.Local.Database;
        _selectedDatabaseName = settingsProvider.Settings.Local.Database;
    }

    public DatabaseMode SelectedDatabaseMode
    {
        get => _selectedDatabaseMode;
        set
        {
            if (_selectedDatabaseMode == value)
                return;

            _selectedDatabaseMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsReadOnlyMode));

            if (_selectedDatabaseMode == DatabaseMode.Local)
            {
                SelectedDatabaseName = SelectedLocalDatabaseName;
            }
            else
            {
                SelectedDatabaseName = SelectedRemoteDatabaseName;
            }
        }
    }

    public bool IsReadOnlyMode => SelectedDatabaseMode == DatabaseMode.Remote;

    public string SelectedRemoteDatabaseName
    {
        get => _selectedRemoteDatabaseName;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || _selectedRemoteDatabaseName == value)
                return;

            _selectedRemoteDatabaseName = value;
            OnPropertyChanged();

            if (SelectedDatabaseMode == DatabaseMode.Remote)
            {
                SelectedDatabaseName = _selectedRemoteDatabaseName;
            }
        }
    }

    public string SelectedLocalDatabaseName
    {
        get => _selectedLocalDatabaseName;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || _selectedLocalDatabaseName == value)
                return;

            _selectedLocalDatabaseName = value;
            OnPropertyChanged();

            if (SelectedDatabaseMode == DatabaseMode.Local)
            {
                SelectedDatabaseName = _selectedLocalDatabaseName;
            }
        }
    }

    public string SelectedDatabaseName
    {
        get => _selectedDatabaseName;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || _selectedDatabaseName == value)
                return;

            _selectedDatabaseName = value;
            OnPropertyChanged();

            if (SelectedDatabaseMode == DatabaseMode.Remote)
            {
                _selectedRemoteDatabaseName = value;
                OnPropertyChanged(nameof(SelectedRemoteDatabaseName));
            }
        }
    }

    public long DataVersion => _dataVersion;

    public void NotifyDataChanged()
    {
        _dataVersion++;
        OnPropertyChanged(nameof(DataVersion));
    }

    public string SelectedAcademicYear
    {
        get => _selectedAcademicYear;
        set
        {
            if (_selectedAcademicYear == value)
                return;

            _selectedAcademicYear = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
}
