using System;
using System.Windows;

namespace LanguageSchoolERP.App.Windows;

public partial class DestructiveActionConfirmationWindow : Window
{
    private readonly string _requiredPhrase;

    public DestructiveActionConfirmationWindow(string title, string message, string requiredPhrase)
    {
        InitializeComponent();

        _requiredPhrase = requiredPhrase ?? string.Empty;

        Title = title;
        HeaderTextBlock.Text = title;
        MessageTextBlock.Text = message;
        InstructionTextBlock.Text = $"Πληκτρολογήστε ακριβώς: {_requiredPhrase}";
    }

    private void ContinueButton_Click(object sender, RoutedEventArgs e)
    {
        var value = ConfirmationTextBox.Text?.Trim() ?? string.Empty;

        if (!string.Equals(value, _requiredPhrase, StringComparison.Ordinal))
        {
            MessageBox.Show(
                "Η επιβεβαίωση δεν ταιριάζει. Η ενέργεια ακυρώθηκε.",
                "Επιβεβαίωση",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }
}
