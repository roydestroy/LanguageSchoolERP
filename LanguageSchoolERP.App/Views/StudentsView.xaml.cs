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
}
