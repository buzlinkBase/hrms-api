namespace Hrms.adms.Models.Entities;

public class CreateBiometricTemplate
{
    public int BioId { get; set; }
    public BiometricType BioType { get; set; }
    public int BioIndex { get; set; }
    public int TemplateSize { get; set; }
    public string TemplateData { get; set; } = string.Empty;
}