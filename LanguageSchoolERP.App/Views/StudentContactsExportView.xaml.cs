using System.Windows;
using System.Windows.Controls;
using LanguageSchoolERP.App.ViewModels;

namespace LanguageSchoolERP.App.Views;

public partial class StudentContactsExportView : UserControl
{
    private readonly StudentContactsExportViewModel _vm;

    public StudentContactsExportView(StudentContactsExportViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _vm.LoadAsync();
    }
}
