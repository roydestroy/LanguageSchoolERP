using System;
using System.Windows;
using LanguageSchoolERP.App.ViewModels;

namespace LanguageSchoolERP.App.Windows;

public partial class AddProgramEnrollmentWindow : Window
{
    private readonly AddProgramEnrollmentViewModel _vm;

    public AddProgramEnrollmentWindow(AddProgramEnrollmentViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        vm.RequestClose += (_, result) =>
        {
            DialogResult = result;
            Close();
        };
    }

    public void Initialize(AddProgramEnrollmentInit init)
    {
        _vm.Initialize(init);
    }
}
