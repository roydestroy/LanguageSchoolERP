using System.Windows;
using System.Windows.Controls;
using LanguageSchoolERP.App.ViewModels;
using LanguageSchoolERP.App.Windows;

namespace LanguageSchoolERP.App.Views;

public partial class AcademicYearsView : UserControl
{
    public AcademicYearsView(AcademicYearsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private async void AddAcademicYearButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AcademicYearsViewModel vm)
        {
            return;
        }

        var dialog = new AddAcademicYearWindow
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            await vm.AddAcademicYearAsync(dialog.AcademicYearName);
        }
    }
}
