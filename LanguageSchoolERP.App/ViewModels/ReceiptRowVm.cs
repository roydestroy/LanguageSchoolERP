namespace LanguageSchoolERP.App.ViewModels;

public class ReceiptRowVm
{
    public string NumberText { get; set; } = "";
    public string DateText { get; set; } = "";
    public string AmountText { get; set; } = "";
    public string MethodText { get; set; } = "";
    public string ProgramText { get; set; } = "";
    public bool HasPdf { get; set; }
    public string PdfPath { get; set; } = "";
}
