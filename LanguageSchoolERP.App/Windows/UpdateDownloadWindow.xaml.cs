using System;
using System.Windows;

namespace LanguageSchoolERP.App.Windows;

public partial class UpdateDownloadWindow : Window
{
    public UpdateDownloadWindow()
    {
        InitializeComponent();
    }

    public void SetIndeterminate(string? details = null)
    {
        Progress.IsIndeterminate = true;
        if (!string.IsNullOrWhiteSpace(details))
            DetailsText.Text = details;
    }

    public void SetProgress(double percent, string? details = null)
    {
        Progress.IsIndeterminate = false;
        Progress.Value = Math.Max(0, Math.Min(100, percent));
        if (!string.IsNullOrWhiteSpace(details))
            DetailsText.Text = details;
    }
}
