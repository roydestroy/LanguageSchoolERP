using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LanguageSchoolERP.App.ViewModels;

public partial class ProgramEnrollmentRowVm : ObservableObject
{
    public Guid EnrollmentId { get; set; }
    public string ProgramText { get; set; } = "";
    public string LevelOrClassText { get; set; } = "";
    public string AgreementTotalText { get; set; } = "";
    public string BooksText { get; set; } = "";
    public string DownPaymentText { get; set; } = "";
    public string InstallmentsText { get; set; } = "";
    public string StatusText { get; set; } = "";
    public string CommentsText { get; set; } = "";
}
