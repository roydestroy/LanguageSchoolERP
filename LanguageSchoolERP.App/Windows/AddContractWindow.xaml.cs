using System.Windows;
using LanguageSchoolERP.App.ViewModels;

namespace LanguageSchoolERP.App.Windows;

public partial class AddContractWindow : Window
{
    private readonly AddContractViewModel _vm;

    public AddContractWindow(AddContractViewModel vm)
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

    public void Initialize(AddContractInit init) => _vm.Initialize(init);
}
