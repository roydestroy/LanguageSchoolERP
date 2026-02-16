using CommunityToolkit.Mvvm.ComponentModel;

namespace LanguageSchoolERP.App.ViewModels;

public partial class PaymentRowVm : ObservableObject
{
    public Guid? PaymentId { get; set; }
    public bool IsSyntheticEntry { get; set; }

    [ObservableProperty] private string typeText = "";
    [ObservableProperty] private string dateText = "";
    [ObservableProperty] private string amountText = "";
    [ObservableProperty] private string method = "";
    [ObservableProperty] private string reasonText = "";
    [ObservableProperty] private string notes = "";
    public string ProgramText { get; set; } = "";
}
