using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LanguageSchoolERP.App.ViewModels;

public partial class StudentRowVm : ObservableObject
{
    public Guid StudentId { get; set; }

    [ObservableProperty] private string fullName = "";
    [ObservableProperty] private string contactLine = "";
    [ObservableProperty] private string yearLabel = "";

    [ObservableProperty] private bool isActive;
    public string ActiveBadgeText => IsActive ? "ACTIVE" : "INACTIVE";
    public Brush ActiveBadgeBackground => IsActive ? new SolidColorBrush(Color.FromRgb(232, 248, 238)) : new SolidColorBrush(Color.FromRgb(243, 245, 247));
    public Brush ActiveBadgeForeground => IsActive ? new SolidColorBrush(Color.FromRgb(23, 111, 61)) : new SolidColorBrush(Color.FromRgb(92, 107, 121));
    public Brush ActiveBadgeBorder => IsActive ? new SolidColorBrush(Color.FromRgb(179, 229, 199)) : new SolidColorBrush(Color.FromRgb(219, 226, 234));

    [ObservableProperty] private decimal balance;
    public string BalanceText => $"{Balance:0.00} €";

    [ObservableProperty] private bool isOverdue;
    public string OverdueBadgeText => IsOverdue ? "OVERDUE" : "OK";
    public Brush OverdueBadgeBackground => IsOverdue ? new SolidColorBrush(Color.FromRgb(253, 237, 237)) : new SolidColorBrush(Color.FromRgb(232, 248, 238));
    public Brush OverdueBadgeForeground => IsOverdue ? new SolidColorBrush(Color.FromRgb(177, 38, 38)) : new SolidColorBrush(Color.FromRgb(23, 111, 61));
    public Brush OverdueBadgeBorder => IsOverdue ? new SolidColorBrush(Color.FromRgb(244, 198, 198)) : new SolidColorBrush(Color.FromRgb(179, 229, 199));

    [ObservableProperty] private bool hasPendingContract;
    public string PendingContractBadgeText => "CONTRACT PENDING";
    public Brush PendingContractBadgeBackground => new SolidColorBrush(Color.FromRgb(255, 246, 229));
    public Brush PendingContractBadgeForeground => new SolidColorBrush(Color.FromRgb(166, 102, 0));
    public Brush PendingContractBadgeBorder => new SolidColorBrush(Color.FromRgb(255, 223, 163));
    public Visibility PendingContractVisibility => HasPendingContract ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty] private bool isExpanded;
    public Visibility ExpandedVisibility => IsExpanded ? Visibility.Visible : Visibility.Collapsed;

    public ObservableCollection<EnrollmentRowVm> Enrollments { get; } = new();

    partial void OnIsExpandedChanged(bool value)
        => OnPropertyChanged(nameof(ExpandedVisibility));

    partial void OnIsActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(ActiveBadgeText));
        OnPropertyChanged(nameof(ActiveBadgeBackground));
        OnPropertyChanged(nameof(ActiveBadgeForeground));
        OnPropertyChanged(nameof(ActiveBadgeBorder));
    }

    partial void OnIsOverdueChanged(bool value)
    {
        OnPropertyChanged(nameof(OverdueBadgeText));
        OnPropertyChanged(nameof(OverdueBadgeBackground));
        OnPropertyChanged(nameof(OverdueBadgeForeground));
        OnPropertyChanged(nameof(OverdueBadgeBorder));
    }

    partial void OnHasPendingContractChanged(bool value)
    {
        OnPropertyChanged(nameof(PendingContractVisibility));
        OnPropertyChanged(nameof(PendingContractBadgeText));
        OnPropertyChanged(nameof(PendingContractBadgeBackground));
        OnPropertyChanged(nameof(PendingContractBadgeForeground));
        OnPropertyChanged(nameof(PendingContractBadgeBorder));
    }
}
