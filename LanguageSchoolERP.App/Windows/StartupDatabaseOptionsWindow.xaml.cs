using System.Windows;

namespace LanguageSchoolERP.App.Windows;

public partial class StartupDatabaseOptionsWindow : Window
{
    public string SelectedDatabaseName { get; private set; } = "FilotheiSchoolERP";
    public string DefaultLocalDatabaseName { get; private set; } = "FilotheiSchoolERP";

    public StartupDatabaseOptionsWindow()
    {
        InitializeComponent();
    }

    public void Initialize(string currentDefaultDatabase, string configuredLocalDatabase)
    {
        DefaultLocalDatabaseName = string.IsNullOrWhiteSpace(configuredLocalDatabase)
            ? "FilotheiSchoolERP"
            : configuredLocalDatabase;

        SelectedDatabaseName = currentDefaultDatabase;
        DefaultLocalRadio.Content = $"Default local ({DefaultLocalDatabaseName})";
        DefaultLocalRadio.IsChecked = currentDefaultDatabase == DefaultLocalDatabaseName;
        FilotheiRadio.IsChecked = currentDefaultDatabase == "FilotheiSchoolERP";
        NeaIoniaRadio.IsChecked = currentDefaultDatabase == "NeaIoniaSchoolERP";
    }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        SelectedDatabaseName = DefaultLocalRadio.IsChecked == true
            ? DefaultLocalDatabaseName
            : NeaIoniaRadio.IsChecked == true
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
