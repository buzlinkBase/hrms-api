using System.ComponentModel.DataAnnotations.Schema;

namespace Hrms.adms.Models.Entities
{
    public class DeviceCommand : BaseEntity
    {
        public required string SN { get; set; }
        [Column(TypeName = "text")]
        public required string Commands { get; set; }
    }
}
