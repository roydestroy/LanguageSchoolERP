using System.Windows;
using LanguageSchoolERP.App.ViewModels;

namespace LanguageSchoolERP.App.Windows;

public partial class NewStudentWindow : Window
{
    public NewStudentWindow(NewStudentViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.RequestClose += (_, result) =>
        {
            DialogResult = result;
            Close();
        };
    }
}
