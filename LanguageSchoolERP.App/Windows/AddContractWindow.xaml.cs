using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    private void CreatedAtDatePicker_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DatePicker datePicker)
            return;

        if (!datePicker.IsDropDownOpen)
            datePicker.IsDropDownOpen = true;
    }
}
