using System.Windows;
using LanguageSchoolERP.App.ViewModels;

namespace LanguageSchoolERP.App.Windows;

public partial class AddPaymentWindow : Window
{
    private readonly AddPaymentViewModel _vm;

    public AddPaymentWindow(AddPaymentViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        _vm.RequestClose += (_, ok) =>
        {
            DialogResult = ok;
            Close();
        };
    }

    public void Initialize(AddPaymentInit init) => _vm.Initialize(init);
}
