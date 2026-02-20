using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LanguageSchoolERP.App.ViewModels;

namespace LanguageSchoolERP.App.Views;

public partial class StudentsView : UserControl
{
    public StudentsView(StudentsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        AddHandler(PreviewMouseDownEvent, new MouseButtonEventHandler(OnPreviewMouseDownOutsideSearch), true);
    }

    private void OnPreviewMouseDownOutsideSearch(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
            return;

        if (IsDescendantOf(source, StudentSearchContainer))
            return;

        if (DataContext is StudentsViewModel vm)
            vm.IsSearchSuggestionsOpen = false;

        Keyboard.ClearFocus();
    }

    private static bool IsDescendantOf(DependencyObject current, DependencyObject parent)
    {
        var node = current;
        while (node is not null)
        {
            if (ReferenceEquals(node, parent))
                return true;

            node = VisualTreeHelper.GetParent(node);
        }

        return false;
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
