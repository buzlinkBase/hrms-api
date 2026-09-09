using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

// Read-only feature/action catalog. Seeded per-tenant (see PermissionCatalogSeederService) rather than
// admin-typed free text — permissions are system-defined, not created through this service.
public class PermissionService : BaseService<Permission>
{
    private readonly PermissionCatalogSeederService _seeder;

    public PermissionService(IUnitOfWorkService uow, PermissionCatalogSeederService seeder) : base(uow)
    {
        _seeder = seeder;
    }

    // Lazily self-heals a tenant provisioned before this feature shipped — the catalog is
    // guaranteed present by the time anyone actually reads it, without a manual backfill step.
    public async Task<List<Permission>> FindAllAsync(CancellationToken token)
    {
        await _seeder.EnsureSeededAsync(token);
        return await GetQueryable().OrderBy(x => x.Module).ThenBy(x => x.Feature).ThenBy(x => x.Action).ToListAsync(token);
    }
}

// Seeds the fixed Module/Feature/Action catalog into a tenant's data. Idempotent — safe to call
// on every new-tenant provisioning run (AccountInitService.Create) and lazily from
// PermissionService.FindAllAsync so an existing tenant (provisioned before this feature shipped)
// self-heals the first time anyone opens the Permissions catalog page.
public class PermissionCatalogSeederService
{
    private readonly IUnitOfWorkService _uow;

    public PermissionCatalogSeederService(IUnitOfWorkService uow)
    {
        _uow = uow;
    }

    private static readonly (string Module, string Feature, string[] Actions)[] Catalog =
    [
        ("Setup", "Organization Setup", ["View", "Create", "Edit", "Delete"]),
        ("Setup", "Workforce Setup", ["View", "Create", "Edit", "Delete"]),
        ("Setup", "Time Shift Setup", ["View", "Create", "Edit", "Delete"]),
        ("Setup", "Deductions & Income Setup", ["View", "Create", "Edit", "Delete"]),
        ("Setup", "Leave Setup", ["View", "Create", "Edit", "Delete"]),
        ("Setup", "Statutory Tables", ["View", "Create", "Edit", "Delete"]),
        ("Setup", "Biometric Setup", ["View", "Create", "Edit", "Delete"]),

        ("Timekeeping", "Upload Attendance", ["View", "Edit", "Export"]),
        ("Timekeeping", "Attendance Manual Entry", ["View", "Edit", "Export"]),
        ("Timekeeping", "Raw Logs", ["View", "Edit", "Export"]),
        ("Timekeeping", "Unregistered Employees", ["View", "Edit", "Export"]),
        ("Timekeeping", "Incomplete Punches", ["View", "Edit", "Export"]),

        ("Change Schedule", "Work Rotation", ["View", "Create", "Approve"]),
        ("Change Schedule", "Change Rest Day", ["View", "Create", "Approve"]),
        ("Change Schedule", "Change Holiday", ["View", "Create", "Approve"]),

        ("DTR Generation", "DTR Master", ["View", "Manage"]),
        ("DTR Generation", "DTR Summary", ["View", "Manage"]),

        ("Payroll Generation", "Payroll Run", ["View", "Create", "Approve", "Export"]),
        ("Payroll Generation", "Payroll Summary", ["View", "Create", "Approve", "Export"]),
        ("Payroll Generation", "13th Month Run", ["View", "Create", "Approve", "Export"]),
        ("Payroll Generation", "Last Pay Run", ["View", "Create", "Approve", "Export"]),
        ("Payroll Generation", "Year-End Adjustment Run", ["View", "Create", "Approve", "Export"]),

        ("Applications", "Leave", ["View", "Approve", "Delete"]),
        ("Applications", "Overtime", ["View", "Approve", "Delete"]),
        ("Applications", "Official Business", ["View", "Approve", "Delete"]),
        ("Applications", "Pass Slip", ["View", "Approve", "Delete"]),
        ("Applications", "Loan/Deduction", ["View", "Approve", "Delete"]),
        ("Applications", "Other Income", ["View", "Approve", "Delete"]),
        ("Applications", "Salary Adjustment", ["View", "Approve", "Delete"]),

        ("Reports", "Government Statutory Reports", ["View", "Export"]),
        ("Reports", "Payroll Reports", ["View", "Export"]),
        ("Reports", "BIR Reports", ["View", "Export"]),

        ("Security", "Users", ["View", "Create", "Edit", "Delete", "Manage"]),
        ("Security", "Roles", ["View", "Create", "Edit", "Delete", "Manage"]),
        ("Security", "Permissions", ["View", "Create", "Edit", "Delete", "Manage"]),
        ("Security", "Audit Trail", ["View", "Create", "Edit", "Delete", "Manage"]),

        ("Employee Portal", "Employee Self-Service Portal", ["View", "Manage"]),
    ];

    public async Task EnsureSeededAsync(CancellationToken token)
    {
        var alreadySeeded = await _uow.Repository.FindAll<Permission>().AnyAsync(token);
        if (alreadySeeded) return;

        var permissions = Catalog
            .SelectMany(row => row.Actions.Select(action => new Permission
            {
                Module = row.Module,
                Feature = row.Feature,
                Action = action,
                Code = $"{row.Feature}:{action}",
                Description = $"{action} access to {row.Feature}",
            }))
            .ToList();

        await _uow.Repository.AddRangeAsync(permissions, token);
        await _uow.CommitChangesAsync("", token);
    }
}

public class RoleService : BaseService<Role>
{
    public RoleService(IUnitOfWorkService uow) : base(uow)
    {
    }

    public Task<List<Role>> FindAllAsync(CancellationToken token)
    {
        return GetQueryable().OrderBy(x => x.Name).ToListAsync(token);
    }

    public Task<Role?> FindOneWithPermissionsAsync(Guid id, CancellationToken token)
    {
        return GetQueryable(x => x.Id == id)
            .Include(x => x.RolePermissions).ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(token);
    }

    public async Task<Role> AddAsync(CreateRole payload, CancellationToken token)
    {
        var role = new Role { Name = payload.Name, Description = payload.Description };
        await CreateAsync(role, token);
        await CommitChangesAsync(token);
        return role;
    }

    public async Task<Role> UpdateAsync(UpdateRole payload, CancellationToken token)
    {
        var role = await GetOneAsync(payload.Id, token) ?? throw new NotFoundException("Role not found");
        role.Name = payload.Name;
        role.Description = payload.Description;
        await ModifyAsync(role, token);
        await CommitChangesAsync(token);
        return role;
    }

    // "No orphaned access" — a role still assigned to at least one user cannot be deleted out
    // from under them; the caller must reassign/remove those users' UserRole rows first.
    public async Task DeleteAsync(Guid id, CancellationToken token)
    {
        var hasAssignments = await _uow.Repository.Find<UserRole>(x => x.RoleId == id).AnyAsync(token);
        if (hasAssignments)
        {
            throw new ValidationException("Cannot delete a role that is still assigned to one or more users.");
        }

        var rolePermissions = await _uow.Repository.Find<RolePermission>(x => x.RoleId == id).ToListAsync(token);
        _uow.Repository.RemoveRange(rolePermissions);
        await RemoveAsync(id, token);
        await CommitChangesAsync(token);
    }

    // Full replace, same semantics as TenantApi's ReplaceRolesAsync — the caller always sends the
    // complete desired permission set, not a diff.
    public async Task SetPermissionsAsync(Guid roleId, List<Guid> permissionIds, CancellationToken token)
    {
        var existing = await _uow.Repository.Find<RolePermission>(x => x.RoleId == roleId).ToListAsync(token);
        _uow.Repository.RemoveRange(existing);

        var toAdd = permissionIds.Distinct()
            .Select(permissionId => new RolePermission { RoleId = roleId, PermissionId = permissionId })
            .ToList();
        if (toAdd.Count > 0)
        {
            await _uow.Repository.AddRangeAsync(toAdd, token);
        }

        await CommitChangesAsync(token);
    }
}

public class UserRoleService
{
    private readonly IUnitOfWorkService _uow;

    public UserRoleService(IUnitOfWorkService uow)
    {
        _uow = uow;
    }

    public Task<List<Role>> GetRolesForUserAsync(Guid userId, CancellationToken token)
    {
        return _uow.Repository.Find<UserRole>(x => x.UserId == userId)
            .Include(x => x.Role)
            .Select(x => x.Role)
            .ToListAsync(token);
    }

    // Full replace, mirroring RoleService.SetPermissionsAsync and TenantApi's own ReplaceRolesAsync.
    public async Task ReplaceRolesAsync(Guid userId, List<Guid> roleIds, CancellationToken token)
    {
        var existing = await _uow.Repository.Find<UserRole>(x => x.UserId == userId).ToListAsync(token);
        _uow.Repository.RemoveRange(existing);

        var toAdd = roleIds.Distinct()
            .Select(roleId => new UserRole { UserId = userId, RoleId = roleId })
            .ToList();
        if (toAdd.Count > 0)
        {
            await _uow.Repository.AddRangeAsync(toAdd, token);
        }

        await _uow.CommitChangesAsync("", token);
    }

    // The primitive a future enforcement phase will call — deduped union of every Code across
    // every role the user holds. Does nothing gating-related yet; only exposed via GET me/permissions.
    public async Task<List<string>> GetEffectivePermissionCodesAsync(Guid userId, CancellationToken token)
    {
        var roleIds = await _uow.Repository.Find<UserRole>(x => x.UserId == userId)
            .Select(x => x.RoleId)
            .ToListAsync(token);
        if (roleIds.Count == 0) return [];

        return await _uow.Repository.Find<RolePermission>(x => roleIds.Contains(x.RoleId))
            .Include(x => x.Permission)
            .Select(x => x.Permission.Code)
            .Distinct()
            .ToListAsync(token);
    }
}
