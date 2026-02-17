using System.Windows;

namespace LanguageSchoolERP.App.Windows;

public partial class AddProgramWindow : Window
{
    public string ProgramName { get; private set; } = "";

    public AddProgramWindow()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += (_, __) => ProgramNameTextBox.Focus();
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        ProgramName = (ProgramName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(ProgramName))
        {
            MessageBox.Show("Παρακαλώ εισάγετε όνομα προγράμματος.");
            return;
        }

        DialogResult = true;
    }
}
