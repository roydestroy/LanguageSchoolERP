using System.Windows;
using System.Windows.Controls;
using LanguageSchoolERP.App.ViewModels;

namespace LanguageSchoolERP.App.Views;

public partial class DatabaseImportView : UserControl
{
    public DatabaseImportView(DatabaseImportViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private async void CheckUpdatesBtn_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        await App.CheckForUpdatesInteractiveAsync(owner, userInitiated: true);
    }

    private void ProgramsBtn_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.NavigateToProgramsFromSettings();
        }
    }

    private void AcademicYearsBtn_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.NavigateToAcademicYearsFromSettings();
        }
    }
}
