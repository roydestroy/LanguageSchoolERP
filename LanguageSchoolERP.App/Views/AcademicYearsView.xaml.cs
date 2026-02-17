using System.Windows;
using System.Windows.Controls;
using LanguageSchoolERP.App.ViewModels;

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
        if (DataContext is AcademicYearsViewModel vm)
        {
            await vm.AddAcademicYearAsync();
        }
    }
}
