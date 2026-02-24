using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace LanguageSchoolERP.App.Windows;

public partial class AddAcademicYearWindow : Window, INotifyPropertyChanged
{
    private readonly string _baseAcademicYearName;
    private int _yearIncrement;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string AcademicYearName => BuildAcademicYearName(_yearIncrement);

    public string IncrementLabel => _yearIncrement == 0
        ? "Θα προστεθεί το αμέσως επόμενο ακαδημαϊκό έτος."
        : $"Προσαύξηση: +{_yearIncrement} έτος/έτη από το επόμενο.";

    public AddAcademicYearWindow(string latestAcademicYearName)
    {
        InitializeComponent();
        _baseAcademicYearName = latestAcademicYearName;
        DataContext = this;
    }

    private string BuildAcademicYearName(int increment)
    {
        if (!TryParseAcademicYear(_baseAcademicYearName, out var startYear, out var endYear))
        {
            return string.Empty;
        }

        var nextStart = startYear + 1 + increment;
        var nextEnd = endYear + 1 + increment;
        return $"{nextStart}-{nextEnd}";
    }

    private static bool TryParseAcademicYear(string? value, out int startYear, out int endYear)
    {
        startYear = 0;
        endYear = 0;

        var parts = (value ?? string.Empty).Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        return int.TryParse(parts[0], out startYear) && int.TryParse(parts[1], out endYear);
    }

    private void IncreaseIncrementButton_Click(object sender, RoutedEventArgs e)
    {
        _yearIncrement++;
        NotifyComputedPropertiesChanged();
    }

    private void DecreaseIncrementButton_Click(object sender, RoutedEventArgs e)
    {
        if (_yearIncrement == 0)
        {
            return;
        }

        _yearIncrement--;
        NotifyComputedPropertiesChanged();
    }

    private void NotifyComputedPropertiesChanged()
    {
        OnPropertyChanged(nameof(AcademicYearName));
        OnPropertyChanged(nameof(IncrementLabel));
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AcademicYearName))
        {
            MessageBox.Show("Δεν βρέθηκε έγκυρο προηγούμενο ακαδημαϊκό έτος για δημιουργία νέου.");
            return;
        }

        DialogResult = true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
