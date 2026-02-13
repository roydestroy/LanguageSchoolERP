using System.Windows;
using LanguageSchoolERP.App.ViewModels;

namespace LanguageSchoolERP.App.Windows;

public partial class CreateContractWindow : Window
{
    private readonly CreateContractViewModel _vm;

    public CreateContractWindow(CreateContractViewModel vm)
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

    public void Initialize(CreateContractInit init) => _vm.Initialize(init);
}
