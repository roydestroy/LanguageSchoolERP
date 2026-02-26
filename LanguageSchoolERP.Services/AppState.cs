using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
    private bool _hasFilotheiLocalDatabase = true;
    private bool _hasNeaIoniaLocalDatabase = true;
    private bool _isLocalModeEnabled = true;
    private bool _isRemoteModeEnabled = true;
    private bool _isDatabaseImportEnabled = true;
    private bool _isTailscaleInstalled = true;

    public AppState(DatabaseAppSettingsProvider settingsProvider)
    {
        _settingsProvider = settingsProvider;

        _selectedRemoteDatabaseName = settingsProvider.Settings.Startup.RemoteDatabase;
        if (string.IsNullOrWhiteSpace(_selectedRemoteDatabaseName))
            _selectedRemoteDatabaseName = settingsProvider.RemoteDatabases.FirstOrDefault()?.Database ?? "FilotheiSchoolERP_View";

        _selectedLocalDatabaseName = settingsProvider.Settings.Startup.LocalDatabase;
        if (string.IsNullOrWhiteSpace(_selectedLocalDatabaseName))
            _selectedLocalDatabaseName = settingsProvider.Settings.Local.Database;

        _selectedDatabaseMode = settingsProvider.Settings.Startup.Mode;
        _selectedDatabaseName = _selectedDatabaseMode == DatabaseMode.Local
            ? _selectedLocalDatabaseName
            : _selectedRemoteDatabaseName;
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

    public bool HasFilotheiLocalDatabase
    {
        get => _hasFilotheiLocalDatabase;
        private set
        {
            if (_hasFilotheiLocalDatabase == value)
                return;

            _hasFilotheiLocalDatabase = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasBothLocalDatabases));
            OnPropertyChanged(nameof(HasAnyLocalDatabase));
            OnPropertyChanged(nameof(AvailableLocalDatabases));
        }
    }

    public bool HasNeaIoniaLocalDatabase
    {
        get => _hasNeaIoniaLocalDatabase;
        private set
        {
            if (_hasNeaIoniaLocalDatabase == value)
                return;

            _hasNeaIoniaLocalDatabase = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasBothLocalDatabases));
            OnPropertyChanged(nameof(HasAnyLocalDatabase));
            OnPropertyChanged(nameof(AvailableLocalDatabases));
        }
    }

    public bool HasBothLocalDatabases => HasFilotheiLocalDatabase && HasNeaIoniaLocalDatabase;
    public bool HasAnyLocalDatabase => HasFilotheiLocalDatabase || HasNeaIoniaLocalDatabase;

    public IReadOnlyList<string> AvailableLocalDatabases
    {
        get
        {
            var available = new List<string>(2);
            if (HasFilotheiLocalDatabase)
                available.Add("FilotheiSchoolERP");
            if (HasNeaIoniaLocalDatabase)
                available.Add("NeaIoniaSchoolERP");
            return available;
        }
    }

    public bool IsLocalModeEnabled
    {
        get => _isLocalModeEnabled;
        private set
        {
            if (_isLocalModeEnabled == value)
                return;

            _isLocalModeEnabled = value;
            OnPropertyChanged();
        }
    }

    public bool IsRemoteModeEnabled
    {
        get => _isRemoteModeEnabled;
        private set
        {
            if (_isRemoteModeEnabled == value)
                return;

            _isRemoteModeEnabled = value;
            OnPropertyChanged();
        }
    }

    public bool IsDatabaseImportEnabled
    {
        get => _isDatabaseImportEnabled;
        private set
        {
            if (_isDatabaseImportEnabled == value)
                return;

            _isDatabaseImportEnabled = value;
            OnPropertyChanged();
        }
    }

    public bool IsTailscaleInstalled
    {
        get => _isTailscaleInstalled;
        private set
        {
            if (_isTailscaleInstalled == value)
                return;

            _isTailscaleInstalled = value;
            OnPropertyChanged();
        }
    }

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
            else
            {
                _selectedLocalDatabaseName = value;
                OnPropertyChanged(nameof(SelectedLocalDatabaseName));
            }
        }
    }

    public string StartupLocalDatabaseName => _settingsProvider.Settings.Startup.LocalDatabase;

    public void UpdateLocalDatabaseAvailability(bool hasFilothei, bool hasNeaIonia)
    {
        HasFilotheiLocalDatabase = hasFilothei;
        HasNeaIoniaLocalDatabase = hasNeaIonia;

        IsLocalModeEnabled = HasAnyLocalDatabase;

        if (!IsLocalModeEnabled && SelectedDatabaseMode == DatabaseMode.Local)
        {
            SelectedDatabaseMode = IsRemoteModeEnabled ? DatabaseMode.Remote : SelectedDatabaseMode;
        }
        else if (SelectedDatabaseMode == DatabaseMode.Local && !AvailableLocalDatabases.Contains(SelectedLocalDatabaseName))
        {
            var fallbackLocalDb = AvailableLocalDatabases.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(fallbackLocalDb))
                SelectedLocalDatabaseName = fallbackLocalDb;
        }
    }

    public void SetDatabaseImportEnabled(bool enabled)
    {
        IsDatabaseImportEnabled = enabled;
    }

    public void SetTailscaleInstalled(bool installed)
    {
        IsTailscaleInstalled = installed;
    }

    public void SetRemoteModeEnabled(bool enabled)
    {
        IsRemoteModeEnabled = enabled;

        if (!IsRemoteModeEnabled && SelectedDatabaseMode == DatabaseMode.Remote)
        {
            if (IsLocalModeEnabled)
                SelectedDatabaseMode = DatabaseMode.Local;
        }

        if (!IsRemoteModeEnabled && !IsLocalModeEnabled)
        {
            SelectedDatabaseMode = DatabaseMode.Remote;
        }
    }


    public void SaveStartupLocalDatabase(string databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            return;

        _settingsProvider.Settings.Startup.Mode = DatabaseMode.Local;
        _settingsProvider.Settings.Startup.LocalDatabase = databaseName;
        _settingsProvider.Save();
    }

    public void SaveCurrentSelectionAsStartupDefault()
    {
        _settingsProvider.Settings.Startup.Mode = SelectedDatabaseMode;
        _settingsProvider.Settings.Startup.LocalDatabase = SelectedLocalDatabaseName;
        _settingsProvider.Settings.Startup.RemoteDatabase = SelectedRemoteDatabaseName;
        _settingsProvider.Save();
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
