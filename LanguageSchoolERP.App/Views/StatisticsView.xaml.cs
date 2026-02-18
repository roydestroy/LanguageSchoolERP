using System.Windows.Controls;
using LanguageSchoolERP.App.ViewModels;

namespace LanguageSchoolERP.App.Views;

public partial class StatisticsView : UserControl
{
    public StatisticsView(StatisticsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
