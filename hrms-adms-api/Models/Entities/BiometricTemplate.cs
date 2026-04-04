using System.ComponentModel.DataAnnotations.Schema;

namespace Hrms.adms.Models.Entities;

public class BiometricTemplate : BaseEntity
{
    public int BioId { get; set; }
    public BiometricType BioType { get; set; }
    public int BioIndex { get; set; }
    public int TemplateSize { get; set; }
    [Column(TypeName = "text")]
    public string TemplateData { get; set; } = string.Empty;
}
