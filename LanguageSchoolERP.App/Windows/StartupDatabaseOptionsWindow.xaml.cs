using System.Windows;

namespace LanguageSchoolERP.App.Windows;

public partial class StartupDatabaseOptionsWindow : Window
{
    public string SelectedDatabaseName { get; private set; } = "FilotheiSchoolERP";

    public StartupDatabaseOptionsWindow()
    {
        InitializeComponent();
    }

    public void Initialize(string currentDefaultDatabase)
    {
        SelectedDatabaseName = currentDefaultDatabase;
        FilotheiRadio.IsChecked = currentDefaultDatabase == "FilotheiSchoolERP";
        NeaIoniaRadio.IsChecked = currentDefaultDatabase == "NeaIoniaSchoolERP";
    }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        SelectedDatabaseName = NeaIoniaRadio.IsChecked == true
            ? "NeaIoniaSchoolERP"
            : "FilotheiSchoolERP";

        DialogResult = true;
        Close();
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
