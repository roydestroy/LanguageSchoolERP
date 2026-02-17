using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LanguageSchoolERP.App.ViewModels;

public partial class EnrollmentRowVm : ObservableObject
{
    [ObservableProperty] private string title = "";
    [ObservableProperty] private string details = "";

    [ObservableProperty] private string agreementText = "";
    [ObservableProperty] private string paidText = "";
    [ObservableProperty] private string balanceText = "";

    [ObservableProperty] private string progressText = "0%";
    [ObservableProperty] private double progressPercent;

    [ObservableProperty] private bool isStopped;
    public Visibility StoppedBadgeVisibility => IsStopped ? Visibility.Visible : Visibility.Collapsed;
    public Brush ProgressBrush => IsStopped
        ? new SolidColorBrush(Color.FromRgb(177, 38, 38))
        : new SolidColorBrush(Color.FromRgb(78, 153, 228));

    partial void OnIsStoppedChanged(bool value)
    {
        OnPropertyChanged(nameof(StoppedBadgeVisibility));
        OnPropertyChanged(nameof(ProgressBrush));
    }
}
