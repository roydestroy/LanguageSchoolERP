using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LanguageSchoolERP.Services;

public class AppState : INotifyPropertyChanged
{
    private string _selectedDatabaseName = "FilotheiSchoolERP";
    private string _selectedAcademicYear = "2025-2026";

    public string SelectedDatabaseName
    {
        get => _selectedDatabaseName;
        set { _selectedDatabaseName = value; OnPropertyChanged(); }
    }

    public string SelectedAcademicYear
    {
        get => _selectedAcademicYear;
        set { _selectedAcademicYear = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
}
