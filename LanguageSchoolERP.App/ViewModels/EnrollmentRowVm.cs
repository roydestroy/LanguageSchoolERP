using System;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LanguageSchoolERP.App.ViewModels;

public partial class EnrollmentRowVm : ObservableObject
{
    public Guid EnrollmentId { get; set; }
    private static readonly Brush DefaultProgressBrush = new SolidColorBrush(Color.FromRgb(78, 153, 228));
    [ObservableProperty] private string title = "";
    [ObservableProperty] private string details = "";

    [ObservableProperty] private string agreementText = "";
    [ObservableProperty] private string paidText = "";
    [ObservableProperty] private string balanceText = "";

    [ObservableProperty] private string progressText = "0%";
    [ObservableProperty] private double progressPercent;

    [ObservableProperty] private bool isStopped;
    [ObservableProperty] private bool canIssuePayment = true;
    [ObservableProperty] private Brush progressBrush = DefaultProgressBrush;

    public Visibility StoppedBadgeVisibility => IsStopped ? Visibility.Visible : Visibility.Collapsed;

    partial void OnIsStoppedChanged(bool value)
        => OnPropertyChanged(nameof(StoppedBadgeVisibility));
}
