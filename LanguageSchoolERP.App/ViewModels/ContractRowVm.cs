namespace LanguageSchoolERP.App.ViewModels;

public class ContractRowVm
{
    public Guid ContractId { get; set; }
    public string CreatedAtText { get; set; } = "";
    public string TemplateText { get; set; } = "";
    public string HasDocxText { get; set; } = "No";
    public string HasPdfText { get; set; } = "No";
    public bool IsPendingPrint { get; set; }
    public string? DocxPath { get; set; }
    public string? PdfPath { get; set; }
}
