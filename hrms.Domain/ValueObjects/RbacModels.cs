namespace Hrms.Domain.ValueObjects;

public class CreateRole
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class UpdateRole : CreateRole
{
    public Guid Id { get; set; }
}

public class PermissionModel
{
    public Guid Id { get; set; }
    public string Module { get; set; } = string.Empty;
    public string Feature { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class RoleModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<PermissionModel> Permissions { get; set; } = new();
}

public class SetRolePermissions
{
    public List<Guid> PermissionIds { get; set; } = new();
}

public class ReplaceUserRoles
{
    public List<Guid> RoleIds { get; set; } = new();
}
