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
}
