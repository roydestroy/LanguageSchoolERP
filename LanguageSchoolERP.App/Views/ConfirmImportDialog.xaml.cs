using System.Windows;
using System.Windows.Controls;

namespace LanguageSchoolERP.App.Views;

public partial class ConfirmImportDialog : Window
{
    private const string ConfirmText = "IMPORT";

    public ConfirmImportDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => ConfirmTextBox.Focus();
    }

    private void ConfirmTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        OkButton.IsEnabled = string.Equals(ConfirmTextBox.Text, ConfirmText, StringComparison.Ordinal);
    }

    private void OkButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
