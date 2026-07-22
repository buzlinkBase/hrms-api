namespace Hrms.adms.Models.Entities;

public class BiometricTemplate : BaseEntity
{
    public int BioId { get; set; }
    public string SN { get; set; } = string.Empty;
    public BiometricType BioType { get; set; }
    public int BioIndex { get; set; }
    public int TemplateSize { get; set; }
    public string TemplateData { get; set; } = string.Empty;
}
