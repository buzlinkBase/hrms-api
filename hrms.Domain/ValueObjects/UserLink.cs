using System.Text.Json.Serialization;

namespace Hrms.Domain.ValueObjects;

/// <summary>Which of this company's records a user account (or pending invite) belongs to.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UserLinkType
{
    Employee,
    Client,
}

/// <summary>The Users page's members: their account ids, plus emails for invites not yet accepted.</summary>
public class UserLinkRequest
{
    public List<Guid> UserIds { get; set; } = [];
    public List<string> Emails { get; set; } = [];
}

public class UserLinkResult
{
    /// <summary>Null when the link was found by email only (an invite not yet accepted).</summary>
    public Guid? UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public UserLinkType Type { get; set; }
    public Guid? EmployeeId { get; set; }
    public string? EmployeeNo { get; set; }
    public string? DepartmentName { get; set; }
    public Guid? ClientId { get; set; }
    public string? ClientCode { get; set; }
    public string? ClientName { get; set; }
    public string? Name { get; set; }
}
