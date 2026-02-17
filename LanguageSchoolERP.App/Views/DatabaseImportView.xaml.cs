using System.Windows.Controls;
using LanguageSchoolERP.App.ViewModels;

namespace LanguageSchoolERP.App.Views;

public partial class DatabaseImportView : UserControl
{
    public DatabaseImportView(DatabaseImportViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
