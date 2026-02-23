using System;
using System.Windows;

namespace LanguageSchoolERP.App.Windows;

public partial class EmailComposeOptionsWindow : Window
{
    public Array RecipientTypes { get; } = Enum.GetValues(typeof(EmailRecipientType));
    public EmailRecipientType SelectedRecipientType { get; set; } = EmailRecipientType.Bcc;
    public bool IncludeStudentEmail { get; set; } = true;
    public bool IncludeFatherEmail { get; set; } = true;
    public bool IncludeMotherEmail { get; set; } = true;

    public EmailComposeOptionsWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        if (!IncludeStudentEmail && !IncludeFatherEmail && !IncludeMotherEmail)
        {
            MessageBox.Show("Επιλέξτε τουλάχιστον μία πηγή email.", "Mailing list", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }
}

public enum EmailRecipientType
{
    To,
    Cc,
    Bcc
}
