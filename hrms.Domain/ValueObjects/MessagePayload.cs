
namespace Hrms.Domain.ValueObjects;
public record TenantDeletedPayload 
{
    public Guid TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
