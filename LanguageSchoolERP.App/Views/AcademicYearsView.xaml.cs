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

    private void AddAcademicYearButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is AcademicYearsViewModel vm && vm.AddAcademicYearCommand.CanExecute(null))
        {
            vm.AddAcademicYearCommand.Execute(null);
        }
    }
}
