using Hrms.Domain.Entities.EmployeeEntities;
using Mapster;

namespace Hrms.Core.Services;

public class EmployeeSettingService(IUnitOfWorkService uow)
    : BaseService<EmployeeSetting>(uow)
{
    public async Task AddAsync(EmployeeSetting model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }

    public async Task UpdateAsync(UpdateEmployeeSetting payload, CancellationToken token)
    {
        var existing = await Context.EmployeeSettings.FindAsync(new object[] { payload.Id }, token);
        if (existing == null)
        {
            throw new NotFoundException("Record not found");
        }
        var salaryType = await Context.Employees
            .Where(x => x.Id == existing.EmployeeId)
            .Select(x => x.SalaryType)
            .FirstOrDefaultAsync(token);
        var noDtrError = NoDtrWindow.Validate(
            payload.IsNoDTRNotRequired, payload.NoDtrCategory, payload.NoDtrEffectiveFrom, payload.NoDtrEffectiveUntil, salaryType);
        if (noDtrError != null)
        {
            throw new ValidationException(noDtrError);
        }
        payload.Adapt(existing);
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);

    }
    public async Task<EmployeeSetting?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneReadOnlyAsync(Id, token);
    }
}

