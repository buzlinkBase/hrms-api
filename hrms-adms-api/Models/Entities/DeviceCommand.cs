namespace Hrms.adms.Models.Entities
{
    public class DeviceCommand : BaseEntity
    {
        public required string SN { get; set; }
        public string CommandType { get; set; } = string.Empty;
        public required string Commands { get; set; }
    }
}
