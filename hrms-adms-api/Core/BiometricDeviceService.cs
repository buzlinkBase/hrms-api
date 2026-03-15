using Hrms.adms.Models.DTO;
using Hrms.adms.Models.Entities;

namespace Hrms.adms.Core;

public class BiometricDeviceService : BaseService<BiometricDevice>
{
    public BiometricDeviceService(IUnitOfWorkService uow) : base(uow)
    {
    }

    public async Task<UpdateBiometricDevice> AddAsync(CreateBiometricDevice payload, CancellationToken token)
    {
        var model = new BiometricDevice
        {
            SN = payload.SN,
            Status = "Active"
        };
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
        return new UpdateBiometricDevice
        {
            Id = model.Id,
            SN = payload.SN,
            Status = model.Status,
        };
    }

    public async Task<UpdateBiometricDevice> UpdateAsync(UpdateBiometricDevice payload, CancellationToken token)
    {
        var model = new BiometricDevice
        {
            Id = payload.Id,
            SN = payload.SN,
            Status = payload.Status
        };
        await ModifyAsync(model, token);
        await CommitChangesAsync(token);
        return new UpdateBiometricDevice
        {
            Id = model.Id,
            SN = payload.SN,
            Status = model.Status,
        };
    }
    public async Task<List<BiometricDevice>> FindAllAsync(CancellationToken token)
    {
        return await GetQueryable()
            .ToListAsync(token);
    }
    public async Task<BiometricDevice?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task<BiometricDevice?> FindSnAsync(string SN, CancellationToken token)
    {
        return await GetQueryable(x => x.SN == SN).FirstOrDefaultAsync(token);
    }
    public async Task DeleteAsync(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);
    }
}

