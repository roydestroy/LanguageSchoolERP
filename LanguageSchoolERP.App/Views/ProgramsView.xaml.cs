using System.Windows;
using System.Windows.Controls;
using LanguageSchoolERP.App.ViewModels;

namespace LanguageSchoolERP.App.Views;

public partial class ProgramsView : UserControl
{
    public ProgramsView(ProgramsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void AddProgramButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ProgramsViewModel vm)
        {
            vm.AddProgramFromInput();
        }
    }
}
