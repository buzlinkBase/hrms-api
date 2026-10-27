using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services.Security;

// The org structure for dynamic approver steps, read straight from a context with its query filters
// off -- ConfidentialAccessScope runs while the request's own filters are still being set up.
public class DbContextOrgLookup : IApproverOrgLookup
{
    private readonly HrmsContext _db;

    public DbContextOrgLookup(HrmsContext db) => _db = db;

    public Task<Employee?> GetEmployeeAsync(Guid id, CancellationToken token) =>
        _db.Employees.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, token);

    public async Task<DepartmentNode?> GetDepartmentAsync(Guid id, CancellationToken token)
    {
        var row = await _db.Departments.IgnoreQueryFilters().AsNoTracking().Where(d => d.Id == id)
            .Select(d => new { d.HeadId, d.ParentDepartmentId }).FirstOrDefaultAsync(token);
        return row == null ? null : new DepartmentNode(row.HeadId, row.ParentDepartmentId);
    }

    public Task<Guid?> GetSectionHeadAsync(Guid id, CancellationToken token) =>
        _db.Sections.IgnoreQueryFilters().AsNoTracking().Where(s => s.Id == id).Select(s => s.HeadId).FirstOrDefaultAsync(token);

    public Task<Guid?> GetBranchHeadAsync(Guid id, CancellationToken token) =>
        _db.Branches.IgnoreQueryFilters().AsNoTracking().Where(b => b.Id == id).Select(b => b.HeadEmployeeId).FirstOrDefaultAsync(token);

    public Task<Guid?> GetAreaHeadAsync(Guid id, CancellationToken token) =>
        _db.Areas.IgnoreQueryFilters().AsNoTracking().Where(a => a.Id == id).Select(a => a.HeadEmployeeId).FirstOrDefaultAsync(token);

    public Task<Guid?> GetClientHeadAsync(Guid id, CancellationToken token) =>
        _db.Clients.IgnoreQueryFilters().AsNoTracking().Where(c => c.Id == id).Select(c => c.HeadEmployeeId).FirstOrDefaultAsync(token);
}
