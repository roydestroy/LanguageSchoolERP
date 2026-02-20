using System.Windows;
using System.Windows.Controls;
using LanguageSchoolERP.App.ViewModels;

namespace LanguageSchoolERP.App.Views;

public partial class StudentsView : UserControl
{
    public StudentsView(StudentsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void ClearStudentSearch_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is StudentsViewModel vm)
        {
            vm.SearchText = string.Empty;
            vm.IsSearchSuggestionsOpen = false;
        }

        StudentSearchTextBox.Focus();
    }

}
