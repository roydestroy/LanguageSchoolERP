using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LanguageSchoolERP.App.ViewModels;

public partial class StudentRowVm : ObservableObject
{
    private static readonly Brush DefaultProgressBrush = new SolidColorBrush(Color.FromRgb(78, 153, 228));
    private static readonly CultureInfo CurrencyCulture = new("el-GR");

    public Guid StudentId { get; set; }

    [ObservableProperty] private string fullName = "";
    [ObservableProperty] private string contactLine = "";
    [ObservableProperty] private string address = "";
    [ObservableProperty] private string yearLabel = "";
    [ObservableProperty] private string enrollmentSummaryText = "";

    [ObservableProperty] private bool isActive;
    [ObservableProperty] private bool hasStoppedProgram;
    [ObservableProperty] private bool hasOnlyStoppedPrograms;
    public string ActiveStatusText => HasOnlyStoppedPrograms ? "Διακοπή" : (IsActive ? "Ενεργός" : "Ανενεργός");

    [ObservableProperty] private decimal balance;
    public string BalanceText => $"{Balance.ToString("#,##0.#", CurrencyCulture)} €";

    [ObservableProperty] private decimal overdueAmount;

    [ObservableProperty] private string progressText = "0%";
    [ObservableProperty] private double progressPercent;
    [ObservableProperty] private Brush progressBrush = DefaultProgressBrush;

    [ObservableProperty] private bool isOverdue;
    public string OverdueBadgeText => "ΛΗΞΙΠΡΟΘΕΣΜΟ";
    public Brush OverdueBadgeBackground => new SolidColorBrush(Color.FromRgb(253, 237, 237));
    public Brush OverdueBadgeForeground => new SolidColorBrush(Color.FromRgb(177, 38, 38));
    public Brush OverdueBadgeBorder => new SolidColorBrush(Color.FromRgb(244, 198, 198));
    public Visibility OverdueBadgeVisibility => IsOverdue ? Visibility.Visible : Visibility.Collapsed;

    public string StoppedBadgeText => "ΔΙΑΚΟΠΗ";
    public Brush StoppedBadgeBackground => new SolidColorBrush(Color.FromRgb(253, 237, 237));
    public Brush StoppedBadgeForeground => new SolidColorBrush(Color.FromRgb(177, 38, 38));
    public Brush StoppedBadgeBorder => new SolidColorBrush(Color.FromRgb(244, 198, 198));
    public Visibility StoppedBadgeVisibility => HasOnlyStoppedPrograms ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty] private bool hasPendingContract;
    public string PendingContractBadgeText => "ΣΥΜΦΩΝΗΤΙΚΟ";
    public Brush PendingContractBadgeBackground => new SolidColorBrush(Color.FromRgb(255, 246, 229));
    public Brush PendingContractBadgeForeground => new SolidColorBrush(Color.FromRgb(166, 102, 0));
    public Brush PendingContractBadgeBorder => new SolidColorBrush(Color.FromRgb(255, 223, 163));
    public Visibility PendingContractVisibility => HasPendingContract ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty] private bool isExpanded;
    [ObservableProperty] private bool areDetailsLoaded;
    [ObservableProperty] private bool isDetailsLoading;
    public Visibility ExpandedVisibility => IsExpanded ? Visibility.Visible : Visibility.Collapsed;

    public ObservableCollection<EnrollmentRowVm> Enrollments { get; } = new();

    partial void OnIsExpandedChanged(bool value)
        => OnPropertyChanged(nameof(ExpandedVisibility));

    partial void OnIsActiveChanged(bool value)
        => OnPropertyChanged(nameof(ActiveStatusText));

    partial void OnHasStoppedProgramChanged(bool value)
        => OnPropertyChanged(nameof(StoppedBadgeVisibility));

    partial void OnHasOnlyStoppedProgramsChanged(bool value)
    {
        OnPropertyChanged(nameof(ActiveStatusText));
        OnPropertyChanged(nameof(StoppedBadgeVisibility));
    }

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
