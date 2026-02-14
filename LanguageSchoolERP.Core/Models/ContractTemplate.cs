namespace LanguageSchoolERP.Core.Models;

public class ContractTemplate
{
    public Guid ContractTemplateId { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "";
    public string BranchKey { get; set; } = "";
    public string TemplateRelativePath { get; set; } = "";
    public bool IsActive { get; set; } = true;

    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
}
