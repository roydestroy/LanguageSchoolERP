using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

    private void StudentsGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source)
        {
            if (FindVisualParent<CheckBox>(source) is not null)
                return;

            var row = FindVisualParent<DataGridRow>(source);
            if (row?.Item is StudentContactsGridRowVm studentRow)
            {
                studentRow.IsSelected = !studentRow.IsSelected;
            }
        }
    }

    private static T? FindVisualParent<T>(DependencyObject child)
        where T : DependencyObject
    {
        var current = child;
        while (current is not null)
        {
            if (current is T typed)
                return typed;

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
