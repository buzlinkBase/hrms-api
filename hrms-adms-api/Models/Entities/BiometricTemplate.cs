using System.ComponentModel.DataAnnotations.Schema;

namespace Hrms.adms.Models.Entities;

public class BiometricTemplate : BaseEntity
{
    public int BioId { get; set; }
    public BiometricType BioType { get; set; }
    /// <summary>
    /// For Finger: 0-9 (Finger Index). For Face: usually 0.
    /// </summary>
    public int BioIndex { get; set; }

    /// <summary>
    /// The 'Size' parameter provided by the ZKTeco device.
    /// Required for successful transfer.
    /// </summary>
    public int TemplateSize { get; set; }
    /// <summary>
    /// The Base64 template string. 
    /// ColumnType "nvarchar(max)" or "longtext" is used for large payloads.
    /// </summary>
    [Column(TypeName = "text")]
    public string TemplateData { get; set; } = string.Empty;
}
