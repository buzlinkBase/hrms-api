namespace Hrms.Domain.Entities;

// Product-feature access catalog: one row per (Feature, Action) pair, e.g. Feature="Payroll Run",
// Action="Approve". Seeded per-tenant by PermissionCatalogSeeder — like every entity here it
// inherits BaseEntity's TenantId, so it isn't a single cross-tenant table, but the seeded content
// is identical for every tenant (the product's features are the same for everyone).
public class Permission : BaseEntity
{
    public string Module { get; set; } = string.Empty;
    public string Feature { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

// A named bundle of permissions an org's admin defines and assigns to users. Tenant-scoped like
// every other entity in this system — each org manages its own set of roles.
public class Role : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

public class RolePermission : BaseEntity
{
    public Guid RoleId { get; set; }
    public virtual Role Role { get; set; } = null!;
    public Guid PermissionId { get; set; }
    public virtual Permission Permission { get; set; } = null!;
}

// UserId is a plain Guid reference with no FK, same convention as Employee.UserId — Users are
// owned by the Auth/Tenant services, not by hrms-api.
public class UserRole : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public virtual Role Role { get; set; } = null!;
}
