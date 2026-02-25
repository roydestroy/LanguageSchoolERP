using System.Windows;

namespace LanguageSchoolERP.App.Windows;

public partial class MigrationProgressWindow : Window
{
    public MigrationProgressWindow()
    {
        InitializeComponent();
    }

    public void SetStatus(string message)
    {
        StatusText.Text = message;
    }
}
