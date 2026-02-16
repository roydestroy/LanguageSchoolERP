using System.Windows.Controls;
using LanguageSchoolERP.App.ViewModels;

namespace LanguageSchoolERP.App.Views;

public partial class ProgramsView : UserControl
{
    public ProgramsView(ProgramsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
