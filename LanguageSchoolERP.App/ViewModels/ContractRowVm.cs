namespace LanguageSchoolERP.App.ViewModels;

public class ContractRowVm
{
    public Guid ContractId { get; set; }
    public string CreatedAtText { get; set; } = "";
    public string ProgramText { get; set; } = "";
    public bool IsPendingPrint { get; set; }
    public string? DocxPath { get; set; }
    public string? PdfPath { get; set; }
}
