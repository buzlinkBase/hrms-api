using System.Text.Json.Serialization;

namespace Hrms.Domain.ValueObjects;

/// <summary>How an email relates to this company's records when inviting it (batch invite modal).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InviteMatchStatus
{
    /// <summary>Employee batch: the employee this email belongs to, not yet linked to an account.</summary>
    Matched,
    /// <summary>Client batch: the email can become a login for the client.</summary>
    Ok,
    /// <summary>Employee batch: no employee record has this email.</summary>
    NoEmployee,
    /// <summary>The employee or client login already has an account.</summary>
    AlreadyLinked,
    /// <summary>Employee batch: the email already belongs to a client login.</summary>
    ClientLogin,
    /// <summary>Client batch: the email belongs to an employee.</summary>
    EmployeeEmail,
}

public class InviteMatchRequest
{
    public List<string> Emails { get; set; } = [];
}

public class InviteMatchResult
{
    public string Email { get; set; } = string.Empty;
    public InviteMatchStatus Status { get; set; }
    public Guid? EmployeeId { get; set; }
    public string? Name { get; set; }
}
