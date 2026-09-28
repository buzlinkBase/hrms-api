using Onepunch.AuditEngine;

namespace Hrms.adms.Models.Entities
{
    // A queued command is an admin's (or the employee-sync worker's) action when it's created or
    // cancelled; the device acknowledging it later is not. The command text itself can carry
    // whole biometric templates (sync commands), so only its type and target device are kept.
    [Audited(Capture = AuditCapture.Create | AuditCapture.Delete)]
    public class DeviceCommand : BaseEntity
    {
        public required string SN { get; set; }
        public string CommandType { get; set; } = string.Empty;
        [AuditIgnore] public required string Commands { get; set; }
    }
}
