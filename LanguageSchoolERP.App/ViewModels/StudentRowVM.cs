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
    public Brush ActiveBadgeBackground => IsActive ? Brushes.SeaGreen : Brushes.Gray;

    [ObservableProperty] private decimal balance;
    public string BalanceText => $"{Balance:0.00} €";

    [ObservableProperty] private bool isOverdue;
    public string OverdueBadgeText => IsOverdue ? "OVERDUE" : "OK";
    public Brush OverdueBadgeBackground => IsOverdue ? Brushes.IndianRed : Brushes.SeaGreen;

    [ObservableProperty] private bool hasPendingContract;
    public string PendingContractBadgeText => "CONTRACT PENDING";
    public Brush PendingContractBadgeBackground => Brushes.DarkOrange;
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
    }

    partial void OnHasPendingContractChanged(bool value)
    {
        OnPropertyChanged(nameof(PendingContractVisibility));
        OnPropertyChanged(nameof(PendingContractBadgeText));
        OnPropertyChanged(nameof(PendingContractBadgeBackground));
    }
}
