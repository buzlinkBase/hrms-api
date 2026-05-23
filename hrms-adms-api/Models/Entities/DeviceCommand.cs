using System.ComponentModel.DataAnnotations.Schema;

namespace Hrms.adms.Models.Entities
{
    public class DeviceCommand : BaseEntity
    {
        public required string SN { get; set; }
        public string CommandType { get; set; } = string.Empty;
        [Column(TypeName = "longtext")]
        public required string Commands  { get; set; }
    }
}
