using System.Windows;

namespace LanguageSchoolERP.App.Windows;

public partial class AddAcademicYearWindow : Window
{
    public string AcademicYearName { get; private set; } = string.Empty;

    public AddAcademicYearWindow()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += (_, __) => AcademicYearNameTextBox.Focus();
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        AcademicYearName = (AcademicYearName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(AcademicYearName))
        {
            MessageBox.Show("Παρακαλώ εισάγετε όνομα ακαδημαϊκού έτους.");
            return;
        }

        DialogResult = true;
    }
}
