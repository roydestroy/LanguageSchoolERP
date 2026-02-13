using System.Windows;
using LanguageSchoolERP.App.ViewModels;

namespace LanguageSchoolERP.App.Windows;

public partial class StudentProfileWindow : Window
{
    private readonly StudentProfileViewModel _vm;

    public StudentProfileWindow(StudentProfileViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        _vm.RequestClose += HandleRequestClose;
        Closed += (_, __) => _vm.RequestClose -= HandleRequestClose;
        DataContext = vm;
    }

    private void HandleRequestClose()
    {
        Close();
    }

    public void Initialize(Guid studentId)
    {
        _vm.Initialize(studentId);
    }
}
