using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace LanguageSchoolERP.App.Windows;

public partial class LocalSqlServerPickerWindow : Window
{
    public string SelectedServer { get; private set; } = string.Empty;

    public LocalSqlServerPickerWindow()
    {
        InitializeComponent();
    }

    public void Initialize(IReadOnlyList<string> servers, string currentServer)
    {
        ServerComboBox.ItemsSource = servers;
        SelectedServer = string.IsNullOrWhiteSpace(currentServer)
            ? servers.FirstOrDefault() ?? string.Empty
            : currentServer;

        ServerComboBox.Text = SelectedServer;
    }

    private void ConfirmClick(object sender, RoutedEventArgs e)
    {
        var selected = ServerComboBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(selected))
        {
            MessageBox.Show(this,
                "Παρακαλώ επιλέξτε SQL Server.",
                "Σύνδεση Βάσης",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        SelectedServer = selected;
        DialogResult = true;
        Close();
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
