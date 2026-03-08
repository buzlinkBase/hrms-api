using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hrms.Domain.ValueObjects;

public record MessagePayload<T> where T : class, new()
{
    public T Data { get; set; }
}


public record TenantCreatedPayload
{
    public Guid TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public record TenantDeletedPayload 
{
    public Guid TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
