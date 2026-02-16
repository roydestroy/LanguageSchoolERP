using System.Windows.Controls;
using LanguageSchoolERP.App.ViewModels;

namespace LanguageSchoolERP.App.Views;

public partial class AcademicYearsView : UserControl
{
    public AcademicYearsView(AcademicYearsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
