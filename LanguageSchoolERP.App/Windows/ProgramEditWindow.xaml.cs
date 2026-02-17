using System.Windows;
using LanguageSchoolERP.App.ViewModels;

namespace LanguageSchoolERP.App.Windows;

public partial class ProgramEditWindow : Window
{
    private readonly ProgramEditViewModel _vm;

    public ProgramEditWindow(ProgramEditViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_vm.TryValidate(out var validationMessage))
        {
            MessageBox.Show(validationMessage);
            return;
        }

        DialogResult = true;
    }
}
