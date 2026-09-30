namespace Hrms.Domain.Entities;

/// <summary>
/// A login account that belongs to a client (one client can have several). Created when a client
/// invitation is accepted (UserOnboarded.ClientId, see UserOnboardedWorker) -- the client
/// counterpart of Employee.UserId.
/// </summary>
public class ClientUserAccount : BaseEntity
{
    public Guid ClientId { get; set; }
    public virtual Client? Client { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
}
