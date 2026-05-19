namespace Hrms.adms.Services;

public class TemplateService : BaseService<BiometricTemplate>
{
    private readonly DeviceService _deviceService;

    public TemplateService(IUnitOfWorkService uow,
        DeviceService deviceService
        ) : base(uow)
    {
        _deviceService = deviceService;
    }
    public async Task AddRangeTemplate(List<CreateBiometricTemplate> models, CancellationToken token)
    {
        foreach (var model in models)
        {
            var existing = await Context.BiometricTemplates
                .FirstOrDefaultAsync(x => x.TenantId == model.TenantId &&
                                         x.BioId == model.BioId &&
                                         x.BioType == model.BioType &&
                                         x.BioIndex == model.BioIndex, token);

            if (existing != null)
            {
                existing.TemplateData = model.TemplateData;
                existing.TemplateSize = model.TemplateSize;
            }
            else
            {
                Context.BiometricTemplates.Add(new BiometricTemplate
                {
                    TenantId = model.TenantId,
                    BioId = model.BioId,
                    BioType = model.BioType,
                    BioIndex = model.BioIndex,
                    TemplateSize = model.TemplateSize,
                    TemplateData = model.TemplateData
                });
            }
        }
        await Context.SaveChangesAsync(token);
        await CommitChangesAsync(token);
    }

    public async Task Transfer(string SN, Guid tenantId, CancellationToken token)
    {
        //TODO create command for transfer
        var device = await _deviceService.FindSnAsync(SN, token);
        if (device == null) return;

        //var command = new DeviceCommandPayload
        //{
        //    CommandMessage = "",
        //    DeviceSN = SN,
        //    TenantId = device.TenantId
        //};
        //await _deviceService.CreateCommand(new List<DeviceCommandPayload> { command });

    }

    public async Task DeleteAsync(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);
    }
}

