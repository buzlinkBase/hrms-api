using Onepunch.AuditEngine;

namespace Hrms.adms.Models.Entities;

// Templates are uploaded by the devices themselves; only an admin deleting one is an action.
[Audited(Capture = AuditCapture.Delete)]
public class BiometricTemplate : BaseEntity
{
    public int BioId { get; set; }
    public string SN { get; set; } = string.Empty;
    public BiometricType BioType { get; set; }
    public int BioIndex { get; set; }
    public int TemplateSize { get; set; }
    [AuditIgnore] public string TemplateData { get; set; } = string.Empty;
}
