using System.Windows;
using System.Windows.Controls;
using LanguageSchoolERP.App.ViewModels;
using LanguageSchoolERP.App.Windows;

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
        if (DataContext is not ProgramsViewModel vm)
        {
            return;
        }

        var dialog = new AddProgramWindow
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            var added = vm.AddProgram(dialog.ProgramName);
            if (!added)
            {
                MessageBox.Show("Το πρόγραμμα υπάρχει ήδη.");
            }
        }
    }
}
