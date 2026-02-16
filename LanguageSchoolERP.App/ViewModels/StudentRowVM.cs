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
    public string ActiveStatusText => IsActive ? "Active" : "Inactive";

    [ObservableProperty] private decimal balance;
    public string BalanceText => $"{Balance:0.00} €";

    [ObservableProperty] private bool isOverdue;
    public string OverdueBadgeText => "OVERDUE";
    public Brush OverdueBadgeBackground => new SolidColorBrush(Color.FromRgb(253, 237, 237));
    public Brush OverdueBadgeForeground => new SolidColorBrush(Color.FromRgb(177, 38, 38));
    public Brush OverdueBadgeBorder => new SolidColorBrush(Color.FromRgb(244, 198, 198));
    public Visibility OverdueBadgeVisibility => IsOverdue ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty] private bool hasPendingContract;
    public string PendingContractBadgeText => "CONTRACT";
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
        => OnPropertyChanged(nameof(ActiveStatusText));

    partial void OnIsOverdueChanged(bool value)
        => OnPropertyChanged(nameof(OverdueBadgeVisibility));

    partial void OnHasPendingContractChanged(bool value)
    {
        OnPropertyChanged(nameof(PendingContractVisibility));
        OnPropertyChanged(nameof(PendingContractBadgeText));
        OnPropertyChanged(nameof(PendingContractBadgeBackground));
        OnPropertyChanged(nameof(PendingContractBadgeForeground));
        OnPropertyChanged(nameof(PendingContractBadgeBorder));
    }
}
